using System;
using System.Collections.Generic;
using System.IO;
using AIUnbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIUnbound.Editor
{
    [InitializeOnLoad]
    public static class Phase3VerticalSliceTools
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleVerticalSlice.unity";
        private const string SmokeActiveKey = "AIUnbound.Phase3Smoke.Active";
        private const string SmokeStageKey = "AIUnbound.Phase3Smoke.Stage";
        private static int smokeFrames;

        static Phase3VerticalSliceTools()
        {
            if (SessionState.GetBool(SmokeActiveKey, false))
                EditorApplication.update += UpdatePlayableSmoke;
        }

        [MenuItem("AI Unbound/Phase 3/Build Vertical Slice Scene")]
        public static void BuildScene()
        {
            ImportTmpEssentials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BattleVerticalSlice", typeof(BattleVerticalSliceBootstrap));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("AI Unbound Phase 3 vertical slice scene built: " + ScenePath);
        }

        [MenuItem("AI Unbound/Phase 3/Run Acceptance Tests")]
        public static void RunAcceptanceTests()
        {
            var passed = new List<string>();
            TestGridPlacement(); passed.Add("1 grid placement");
            TestDeterminism(); passed.Add("2 deterministic combat");
            TestTargetRenewal(); passed.Add("3 target renewal");
            TestVictory(); passed.Add("4 victory condition");
            TestDefeat(); passed.Add("5 defeat condition");
            TestMinimumDamage(); passed.Add("6 minimum damage");
            TestManualSkillTimeline(); passed.Add("7 manual skill timeline");
            TestCooldown(); passed.Add("8 cooldown");
            Debug.Log("AI Unbound Phase 3 acceptance passed: " + string.Join(", ", passed));
        }

        [MenuItem("AI Unbound/Phase 3/Run Playable Smoke Test")]
        public static void RunPlayableSmokeTest()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SmokeActiveKey, true);
            SessionState.SetInt(SmokeStageKey, 0);
            smokeFrames = 0;
            EditorApplication.update -= UpdatePlayableSmoke;
            EditorApplication.update += UpdatePlayableSmoke;
            EditorApplication.EnterPlaymode();
        }

        private static void UpdatePlayableSmoke()
        {
            if (!SessionState.GetBool(SmokeActiveKey, false))
            {
                EditorApplication.update -= UpdatePlayableSmoke;
                return;
            }
            if (!EditorApplication.isPlaying) return;

            try
            {
                smokeFrames++;
                if (smokeFrames < 5) return;
                var manager = UnityEngine.Object.FindFirstObjectByType<BattleManager>();
                Require(manager != null && manager.Simulation != null, "Playable scene did not initialize BattleManager.");
                Require(UnityEngine.Object.FindFirstObjectByType<BattleUIController>() != null, "Playable scene did not initialize BattleUIController.");
                Require(UnityEngine.Object.FindObjectsByType<CharacterEntity>(FindObjectsSortMode.None).Length == 6,
                    "Playable scene did not create all six combat views.");

                if (SessionState.GetInt(SmokeStageKey, 0) == 0)
                {
                    Require(manager.Simulation.State == BattleState.Preparation, "Playable scene did not reach Preparation.");
                    manager.StartBattle();
                    SessionState.SetInt(SmokeStageKey, 1);
                    smokeFrames = 0;
                    return;
                }

                Require(manager.Simulation.State == BattleState.Fighting || manager.Simulation.State == BattleState.BattleEnd,
                    "Playable scene did not enter combat.");
                Debug.Log("AI Unbound Phase 3 playable smoke passed: Preparation -> Fighting with runtime UI initialized.");
                FinishPlayableSmoke(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishPlayableSmoke(1);
            }
        }

        private static void FinishPlayableSmoke(int exitCode)
        {
            SessionState.SetBool(SmokeActiveKey, false);
            SessionState.SetInt(SmokeStageKey, 0);
            EditorApplication.update -= UpdatePlayableSmoke;
            Time.timeScale = 1f;
            EditorApplication.Exit(exitCode);
        }

        private static void ImportTmpEssentials()
        {
            if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") != null) return;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
            if (package == null) throw new InvalidOperationException("Could not locate the uGUI package.");
            var essentials = Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(essentials, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") == null)
                throw new InvalidOperationException("TMP Essential Resources import failed.");
        }

        private static void TestGridPlacement()
        {
            var simulation = CreateSimulation();
            var seen = new HashSet<string>();
            foreach (var unit in simulation.Units)
            {
                if (unit.Config.team != Team.Player) continue;
                var key = unit.Config.position.row + ":" + unit.Config.position.column;
                Require(seen.Add(key), "Player grid contains overlapping units.");
                Require(simulation.Grid.IsOccupied(Team.Player, unit.Config.position), "Player slot is not occupied.");
            }
            Require(seen.Count == 3, "Expected three deployed player characters.");
        }

        private static void TestDeterminism()
        {
            var first = RunToEnd(CreateSimulation());
            var second = RunToEnd(CreateSimulation());
            Require(first.Result == second.Result, "Deterministic result mismatch.");
            Require(string.Join("|", first.EventLog) == string.Join("|", second.EventLog), "Deterministic event timeline mismatch.");
        }

        private static void TestTargetRenewal()
        {
            var simulation = new BattleSimulation(1, 10f);
            var actor = simulation.AddUnit(TestUnit("actor", Team.Player, 0, 1));
            var first = simulation.AddUnit(TestUnit("first", Team.Enemy, 0, 1));
            var second = simulation.AddUnit(TestUnit("second", Team.Enemy, 1, 1));
            simulation.FinishInitialization();
            simulation.StartBattle();
            Require(simulation.SelectAttackTarget(actor).InstanceId == first.InstanceId, "Initial target selection is incorrect.");
            simulation.ApplyTestDamage(first.InstanceId, first.CurrentHp);
            Require(simulation.SelectAttackTarget(actor).InstanceId == second.InstanceId, "Target did not renew after death.");
        }

        private static void TestVictory()
        {
            var simulation = CreateSimulation();
            simulation.StartBattle();
            foreach (var unit in simulation.Units)
                if (unit.Config.team == Team.Enemy) simulation.ApplyTestDamage(unit.InstanceId, unit.CurrentHp);
            simulation.EvaluateEndConditions();
            Require(simulation.State == BattleState.BattleEnd && simulation.Result == BattleResult.Victory, "Victory was not immediate.");
        }

        private static void TestDefeat()
        {
            var simulation = CreateSimulation();
            simulation.StartBattle();
            foreach (var unit in simulation.Units)
                if (unit.Config.team == Team.Player) simulation.ApplyTestDamage(unit.InstanceId, unit.CurrentHp);
            simulation.EvaluateEndConditions();
            Require(simulation.State == BattleState.BattleEnd && simulation.Result == BattleResult.Defeat, "Defeat was not immediate.");
        }

        private static void TestMinimumDamage()
        {
            Require(CombatMath.Damage(10, 1f, 999, false) == 1, "Damage must be at least one.");
        }

        private static void TestManualSkillTimeline()
        {
            var gameObject = new GameObject("AcceptanceBattleManager");
            try
            {
                var manager = gameObject.AddComponent<BattleManager>();
                manager.Initialize(BattleVerticalSliceBootstrap.CreateStage());
                manager.StartBattle();
                var owner = FindUnit(manager.Simulation, "aegis");
                var boss = FindUnit(manager.Simulation, "overseer");
                Require(manager.BeginManualSkill(owner.InstanceId), "Manual skill could not begin.");
                Require(Math.Abs(Time.timeScale - 0.1f) < 0.001f, "Manual selection did not enter slow motion.");
                Require(manager.ResolveManualSkill(boss.InstanceId), "Manual skill could not resolve.");
                Require(Math.Abs(Time.timeScale - 1f) < 0.001f, "Time scale did not recover after targeting.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                Time.timeScale = 1f;
            }
        }

        private static void TestCooldown()
        {
            var simulation = CreateSimulation();
            simulation.StartBattle();
            var owner = FindUnit(simulation, "aegis");
            var boss = FindUnit(simulation, "overseer");
            Require(simulation.BeginManualSkill(owner.InstanceId), "Skill should be initially ready.");
            Require(simulation.ResolveManualSkill(boss.InstanceId), "Skill should resolve on the boss.");
            Require(owner.SkillCooldown > 0f && !simulation.CanUseManualSkill(owner.InstanceId), "Cooldown did not disable skill.");
            for (var i = 0; i < 61 && simulation.State == BattleState.Fighting; i++) simulation.Tick();
            Require(owner.SkillCooldown <= 0f, "Cooldown did not reach zero.");
        }

        private static BattleSimulation CreateSimulation()
        {
            var simulation = new BattleSimulation(24680, 45f);
            foreach (var config in BattleVerticalSliceBootstrap.CreateStage()) simulation.AddUnit(config);
            simulation.FinishInitialization();
            return simulation;
        }

        private static BattleSimulation RunToEnd(BattleSimulation simulation)
        {
            simulation.StartBattle();
            for (var i = 0; i < 1000 && simulation.State == BattleState.Fighting; i++) simulation.Tick();
            Require(simulation.State == BattleState.BattleEnd, "Battle did not terminate.");
            return simulation;
        }

        private static CombatUnit FindUnit(BattleSimulation simulation, string id)
        {
            foreach (var unit in simulation.Units)
                if (unit.Config.id == id) return unit;
            throw new InvalidOperationException("Missing unit: " + id);
        }

        private static CombatUnitConfig TestUnit(string id, Team team, int row, int column)
        {
            return new CombatUnitConfig
            {
                id = id,
                displayName = id,
                team = team,
                role = CombatRole.Damage,
                maxHp = 100,
                attack = 10,
                defense = 0,
                attackInterval = 1f,
                range = 9,
                position = new GridPosition(row, column)
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Phase 3 acceptance failed: " + message);
        }
    }
}
