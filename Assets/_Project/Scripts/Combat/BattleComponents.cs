using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AIUnbound.Combat
{
    public sealed class BattleManager : MonoBehaviour
    {
        private float accumulator;
        private BattleState lastState;

        public BattleSimulation Simulation { get; private set; }
        public event Action Changed;

        public void Initialize(IEnumerable<CombatUnitConfig> configs, int seed = 13579, float timeLimit = 45f)
        {
            Simulation = new BattleSimulation(seed, timeLimit);
            foreach (var config in configs) Simulation.AddUnit(config);
            Simulation.FinishInitialization();
            lastState = Simulation.State;
            NotifyChanged();
        }

        public void StartBattle()
        {
            Simulation.StartBattle();
            NotifyChanged();
        }

        public bool BeginManualSkill(int ownerId)
        {
            if (!Simulation.BeginManualSkill(ownerId)) return false;
            Time.timeScale = 0.1f;
            NotifyChanged();
            return true;
        }

        public bool ResolveManualSkill(int targetId)
        {
            if (!Simulation.ResolveManualSkill(targetId)) return false;
            Time.timeScale = 1f;
            NotifyChanged();
            return true;
        }

        public void CancelManualSkill()
        {
            Simulation.CancelManualSkill();
            Time.timeScale = 1f;
            NotifyChanged();
        }

        public bool MoveUnit(int instanceId, GridPosition destination)
        {
            var moved = Simulation.MoveUnit(instanceId, destination);
            if (moved) NotifyChanged();
            return moved;
        }

        public void StepForAcceptanceTest()
        {
            Simulation.Tick();
            DetectStateChange();
        }

        private void Update()
        {
            if (Simulation == null || Simulation.State != BattleState.Fighting) return;
            accumulator += Time.deltaTime;
            while (accumulator >= BattleSimulation.TickSeconds)
            {
                accumulator -= BattleSimulation.TickSeconds;
                Simulation.Tick();
                DetectStateChange();
                if (Simulation.State != BattleState.Fighting) break;
            }
            NotifyChanged();
        }

        private void DetectStateChange()
        {
            if (lastState == Simulation.State) return;
            lastState = Simulation.State;
            if (Simulation.State == BattleState.BattleEnd) Time.timeScale = 1f;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (Changed != null) Changed();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }

    public sealed class GridManager : MonoBehaviour
    {
        private readonly RectTransform[,] playerSlots = new RectTransform[3, 3];
        private readonly RectTransform[,] enemySlots = new RectTransform[3, 3];

        public void Build(RectTransform playerRoot, RectTransform enemyRoot)
        {
            BuildSide(playerRoot, playerSlots, new Color(0.10f, 0.22f, 0.38f, 0.7f));
            BuildSide(enemyRoot, enemySlots, new Color(0.38f, 0.12f, 0.16f, 0.7f));
        }

        public RectTransform GetSlot(Team team, GridPosition position)
        {
            return (team == Team.Player ? playerSlots : enemySlots)[position.row, position.column];
        }

        public GridPosition NextOpenPlayerSlot(BattleSimulation simulation, GridPosition current)
        {
            var start = current.row * 3 + current.column;
            for (var offset = 1; offset <= 9; offset++)
            {
                var index = (start + offset) % 9;
                var candidate = new GridPosition(index / 3, index % 3);
                if (!simulation.Grid.IsOccupied(Team.Player, candidate)) return candidate;
            }
            return current;
        }

        private static void BuildSide(RectTransform root, RectTransform[,] slots, Color color)
        {
            var grid = root.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(250f, 170f);
            grid.spacing = new Vector2(22f, 22f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(20, 20, 20, 20);

            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                var slot = new GameObject("Slot_" + row + "_" + column, typeof(RectTransform), typeof(Image));
                var rect = slot.GetComponent<RectTransform>();
                rect.SetParent(root, false);
                slot.GetComponent<Image>().color = color;
                slots[row, column] = rect;
            }
        }
    }

    public sealed class CharacterEntity : MonoBehaviour
    {
        private CombatUnit unit;
        private RectTransform rect;
        private TMP_Text label;
        private Slider hp;
        private Slider mp;
        private Image background;
        private Button button;

        public int InstanceId { get { return unit.InstanceId; } }
        public CombatUnit Unit { get { return unit; } }

        public void Bind(CombatUnit combatUnit, RectTransform slot, TMP_FontAsset font, Action<int> clicked)
        {
            unit = combatUnit;
            rect = GetComponent<RectTransform>();
            rect.SetParent(slot, false);
            rect.anchorMin = new Vector2(0.08f, 0.08f);
            rect.anchorMax = new Vector2(0.92f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            background = gameObject.AddComponent<Image>();
            background.color = unit.Config.team == Team.Player
                ? new Color(0.18f, 0.55f, 0.88f, 1f)
                : new Color(0.82f, 0.25f, 0.30f, 1f);
            button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => clicked(unit.InstanceId));

            label = UIElementFactory.Text(transform, "Name", unit.Config.displayName, 27, font);
            UIElementFactory.Stretch(label.rectTransform, 0.05f, 0.42f, 0.95f, 0.95f);
            hp = UIElementFactory.Slider(transform, "HP", new Color(0.15f, 0.88f, 0.30f));
            UIElementFactory.Stretch((RectTransform)hp.transform, 0.06f, 0.22f, 0.94f, 0.38f);
            mp = UIElementFactory.Slider(transform, "MP", new Color(0.20f, 0.65f, 1f));
            UIElementFactory.Stretch((RectTransform)mp.transform, 0.06f, 0.05f, 0.94f, 0.19f);
            Refresh(false);
        }

        public void MoveTo(RectTransform slot)
        {
            rect.SetParent(slot, false);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void Refresh(bool targetable)
        {
            if (unit == null) return;
            hp.value = unit.HpRatio;
            mp.value = unit.Config.maxMp <= 0 ? 0f : (float)unit.CurrentMp / unit.Config.maxMp;
            label.text = unit.Config.displayName + "\n" + unit.CurrentHp + "/" + unit.Config.maxHp;
            background.color = !unit.IsAlive ? new Color(0.2f, 0.2f, 0.2f, 0.75f) :
                targetable ? new Color(1f, 0.75f, 0.12f, 1f) :
                unit.Config.team == Team.Player ? new Color(0.18f, 0.55f, 0.88f, 1f) : new Color(0.82f, 0.25f, 0.30f, 1f);
            button.interactable = unit.IsAlive;
        }
    }

    public sealed class SkillController : MonoBehaviour
    {
        private BattleManager battle;
        private int ownerId;

        public void Bind(BattleManager battleManager, int combatantId)
        {
            battle = battleManager;
            ownerId = combatantId;
        }

        public void Use()
        {
            battle.BeginManualSkill(ownerId);
        }
    }

    public sealed class BattleUIController : MonoBehaviour
    {
        private readonly Dictionary<int, CharacterEntity> entities = new Dictionary<int, CharacterEntity>();
        private readonly Dictionary<int, Button> skillButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, TMP_Text> skillLabels = new Dictionary<int, TMP_Text>();
        private BattleManager battle;
        private GridManager grid;
        private TMP_FontAsset font;
        private TMP_Text status;
        private TMP_Text instruction;
        private Button startButton;
        private Button cancelButton;
        private GameObject resultPanel;
        private TMP_Text resultText;
        private int selectedSkillOwner = -1;

        public void Build(BattleManager battleManager, GridManager gridManager)
        {
            battle = battleManager;
            grid = gridManager;
            font = UIElementFactory.CreateRuntimeFont();

            var canvasObject = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var background = UIElementFactory.Panel(canvasObject.transform, "Background", new Color(0.035f, 0.045f, 0.075f, 1f));
            UIElementFactory.Stretch(background, 0f, 0f, 1f, 1f);

            status = UIElementFactory.Text(background, "Status", "PREPARATION", 42, font);
            UIElementFactory.Stretch(status.rectTransform, 0.05f, 0.92f, 0.95f, 0.985f);
            instruction = UIElementFactory.Text(background, "Instruction", "Tap an ally to move · Start when ready", 27, font);
            UIElementFactory.Stretch(instruction.rectTransform, 0.05f, 0.865f, 0.95f, 0.92f);

            var enemyRoot = UIElementFactory.Panel(background, "EnemyGrid", new Color(0.08f, 0.06f, 0.09f, 0.8f));
            UIElementFactory.Stretch(enemyRoot, 0.09f, 0.50f, 0.91f, 0.84f);
            var playerRoot = UIElementFactory.Panel(background, "PlayerGrid", new Color(0.05f, 0.08f, 0.12f, 0.8f));
            UIElementFactory.Stretch(playerRoot, 0.09f, 0.15f, 0.91f, 0.49f);
            grid.Build(playerRoot, enemyRoot);

            foreach (var unit in battle.Simulation.Units)
            {
                var viewObject = new GameObject("Unit_" + unit.Config.id, typeof(RectTransform));
                var entity = viewObject.AddComponent<CharacterEntity>();
                entity.Bind(unit, grid.GetSlot(unit.Config.team, unit.Config.position), font, OnEntityClicked);
                entities.Add(unit.InstanceId, entity);
            }

            startButton = UIElementFactory.Button(background, "StartBattle", "START BATTLE", font, battle.StartBattle);
            UIElementFactory.Stretch((RectTransform)startButton.transform, 0.35f, 0.055f, 0.65f, 0.12f);
            cancelButton = UIElementFactory.Button(background, "CancelSkill", "CANCEL", font, CancelSkill);
            UIElementFactory.Stretch((RectTransform)cancelButton.transform, 0.35f, 0.055f, 0.65f, 0.12f);

            var skillRoot = UIElementFactory.Panel(background, "Skills", new Color(0f, 0f, 0f, 0f));
            UIElementFactory.Stretch(skillRoot, 0.05f, 0.018f, 0.95f, 0.13f);
            var layout = skillRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            foreach (var unit in battle.Simulation.Units)
            {
                if (unit.Config.team != Team.Player) continue;
                var owner = unit;
                var button = UIElementFactory.Button(skillRoot, "Skill_" + owner.Config.id, owner.Config.displayName + " SKILL", font,
                    () => SelectSkill(owner.InstanceId));
                skillButtons.Add(owner.InstanceId, button);
                skillLabels.Add(owner.InstanceId, button.GetComponentInChildren<TMP_Text>());
            }

            resultPanel = UIElementFactory.Panel(background, "Result", new Color(0.02f, 0.02f, 0.03f, 0.94f)).gameObject;
            UIElementFactory.Stretch((RectTransform)resultPanel.transform, 0.15f, 0.38f, 0.85f, 0.62f);
            resultText = UIElementFactory.Text(resultPanel.transform, "ResultText", string.Empty, 64, font);
            UIElementFactory.Stretch(resultText.rectTransform, 0f, 0f, 1f, 1f);
            resultPanel.SetActive(false);

            battle.Changed += Refresh;
            Refresh();
        }

        private void SelectSkill(int ownerId)
        {
            if (!battle.BeginManualSkill(ownerId)) return;
            selectedSkillOwner = ownerId;
            Refresh();
        }

        private void OnEntityClicked(int instanceId)
        {
            var unit = battle.Simulation.Find(instanceId);
            if (battle.Simulation.State == BattleState.Preparation && unit.Config.team == Team.Player)
            {
                var next = grid.NextOpenPlayerSlot(battle.Simulation, unit.Config.position);
                if (battle.MoveUnit(instanceId, next)) entities[instanceId].MoveTo(grid.GetSlot(Team.Player, next));
                return;
            }

            if (battle.Simulation.State != BattleState.PlayerSkillSelection || selectedSkillOwner < 0) return;
            if (battle.ResolveManualSkill(instanceId)) selectedSkillOwner = -1;
        }

        private void CancelSkill()
        {
            selectedSkillOwner = -1;
            battle.CancelManualSkill();
        }

        private void Refresh()
        {
            if (battle == null || battle.Simulation == null) return;
            var simulation = battle.Simulation;
            status.text = simulation.State + "  " + simulation.ElapsedSeconds.ToString("0.0") + " / " + simulation.TimeLimitSeconds.ToString("0") + "s";
            startButton.gameObject.SetActive(simulation.State == BattleState.Preparation);
            cancelButton.gameObject.SetActive(simulation.State == BattleState.PlayerSkillSelection);

            if (simulation.State == BattleState.Preparation)
                instruction.text = "Tap an ally to move · Start when ready";
            else if (simulation.State == BattleState.PlayerSkillSelection)
                instruction.text = "Select a skill target";
            else instruction.text = "Basic attacks are automatic · Boss skills are manual";

            foreach (var pair in entities)
            {
                var targetable = false;
                if (simulation.State == BattleState.PlayerSkillSelection && selectedSkillOwner >= 0)
                {
                    var owner = simulation.Find(selectedSkillOwner);
                    targetable = owner.Config.role == CombatRole.Healer
                        ? pair.Value.Unit.Config.team == owner.Config.team
                        : pair.Value.Unit.Config.team != owner.Config.team;
                }
                pair.Value.Refresh(targetable);
            }

            foreach (var pair in skillButtons)
            {
                var unit = simulation.Find(pair.Key);
                pair.Value.gameObject.SetActive(simulation.State == BattleState.Fighting || simulation.State == BattleState.PlayerSkillSelection);
                pair.Value.interactable = simulation.CanUseManualSkill(pair.Key);
                skillLabels[pair.Key].text = unit.Config.displayName + "\n" +
                    (unit.SkillCooldown > 0f ? unit.SkillCooldown.ToString("0.0") + "s" : unit.CurrentMp + "/" + unit.Config.maxMp + " MP");
            }

            if (simulation.State == BattleState.BattleEnd)
            {
                resultPanel.SetActive(true);
                resultText.text = simulation.Result == BattleResult.Victory ? "VICTORY" : "DEFEAT\n" + simulation.Result;
            }
        }

        private void OnDestroy()
        {
            if (battle != null) battle.Changed -= Refresh;
        }
    }

    internal static class UIElementFactory
    {
        public static TMP_FontAsset CreateRuntimeFont()
        {
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null) throw new InvalidOperationException("TMP Essential Resources are missing.");
            return defaultFont;
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel.GetComponent<RectTransform>();
        }

        public static TMP_Text Text(Transform parent, string name, string value, float size, TMP_FontAsset font)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = value;
            text.enableWordWrapping = false;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(parent, name, new Color(0.18f, 0.38f, 0.66f, 1f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            var text = Text(rect, "Label", label, 30f, font);
            Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        public static Slider Slider(Transform parent, string name, Color fillColor)
        {
            var root = Panel(parent, name, new Color(0.08f, 0.08f, 0.1f, 1f));
            var slider = root.gameObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var fill = Panel(root, "Fill", fillColor);
            Stretch(fill, 0f, 0f, 1f, 1f);
            slider.fillRect = fill;
            return slider;
        }

        public static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
