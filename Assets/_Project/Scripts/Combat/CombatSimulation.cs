using System;
using System.Collections.Generic;

namespace AIUnbound.Combat
{
    public enum BattleState { Init, Preparation, BattleStart, Fighting, PlayerSkillSelection, BattleEnd }
    public enum BattleResult { None, Victory, Defeat, Timeout }
    public enum Team { Player, Enemy }
    public enum CombatRole { Tank, Damage, Healer, Boss }

    [Serializable]
    public struct GridPosition
    {
        public int row;
        public int column;

        public GridPosition(int row, int column)
        {
            this.row = row;
            this.column = column;
        }
    }

    [Serializable]
    public sealed class CombatUnitConfig
    {
        public string id;
        public string displayName;
        public Team team;
        public CombatRole role;
        public int maxHp;
        public int attack;
        public int defense;
        public int maxMp = 100;
        public int mpPerTick = 2;
        public float attackInterval = 1f;
        public int range = 9;
        public float basicMultiplier = 1f;
        public float critChance;
        public float manualSkillMultiplier = 2f;
        public float manualSkillCooldown = 5f;
        public bool hasTaunt;
        public bool isBoss;
        public bool areaAttack;
        public GridPosition position;
    }

    public sealed class CombatUnit
    {
        public readonly CombatUnitConfig Config;
        public readonly int InstanceId;
        public int CurrentHp;
        public int CurrentMp;
        public float AttackTimer;
        public float SkillCooldown;
        public int GroggyTicks;
        public int CurrentTargetId = -1;

        public CombatUnit(CombatUnitConfig config, int instanceId)
        {
            Config = config;
            InstanceId = instanceId;
            CurrentHp = config.maxHp;
            CurrentMp = config.team == Team.Player ? config.maxMp : 0;
            AttackTimer = config.attackInterval;
        }

        public bool IsAlive { get { return CurrentHp > 0; } }
        public float HpRatio { get { return Config.maxHp <= 0 ? 0f : (float)CurrentHp / Config.maxHp; } }
    }

    public static class CombatMath
    {
        public static int Damage(int attack, float multiplier, int defense, bool critical)
        {
            var value = Math.Max(1, Round(attack * multiplier) - defense);
            return critical ? Math.Max(1, Round(value * 1.5f)) : value;
        }

        public static int Healing(int attack, float multiplier)
        {
            return Math.Max(0, Round(attack * multiplier));
        }

        private static int Round(float value)
        {
            return (int)Math.Floor(value + 0.5f);
        }
    }

    public sealed class GridModel
    {
        private readonly int[] playerSlots = new int[9];
        private readonly int[] enemySlots = new int[9];

        public GridModel()
        {
            Clear(playerSlots);
            Clear(enemySlots);
        }

        public bool TryOccupy(Team team, GridPosition position, int instanceId)
        {
            if (!IsValid(position)) return false;
            var slots = team == Team.Player ? playerSlots : enemySlots;
            var index = position.row * 3 + position.column;
            if (slots[index] >= 0) return false;
            slots[index] = instanceId;
            return true;
        }

        public void Vacate(Team team, GridPosition position)
        {
            if (!IsValid(position)) return;
            (team == Team.Player ? playerSlots : enemySlots)[position.row * 3 + position.column] = -1;
        }

        public bool IsOccupied(Team team, GridPosition position)
        {
            if (!IsValid(position)) return false;
            return (team == Team.Player ? playerSlots : enemySlots)[position.row * 3 + position.column] >= 0;
        }

        private static bool IsValid(GridPosition position)
        {
            return position.row >= 0 && position.row < 3 && position.column >= 0 && position.column < 3;
        }

        private static void Clear(int[] slots)
        {
            for (var i = 0; i < slots.Length; i++) slots[i] = -1;
        }
    }

    public sealed class BattleSimulation
    {
        public const float TickSeconds = 0.1f;

        private readonly List<CombatUnit> units = new List<CombatUnit>();
        private readonly Random random;
        private int nextInstanceId = 1;
        private int selectedSkillOwnerId = -1;

        public readonly GridModel Grid = new GridModel();
        public readonly List<string> EventLog = new List<string>();
        public BattleState State { get; private set; }
        public BattleResult Result { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float TimeLimitSeconds { get; private set; }
        public IReadOnlyList<CombatUnit> Units { get { return units; } }

        public BattleSimulation(int randomSeed, float timeLimitSeconds = 90f)
        {
            random = new Random(randomSeed);
            TimeLimitSeconds = timeLimitSeconds;
            State = BattleState.Init;
        }

        public CombatUnit AddUnit(CombatUnitConfig config)
        {
            if (State != BattleState.Init && State != BattleState.Preparation)
                throw new InvalidOperationException("Units can only be added before battle.");
            var unit = new CombatUnit(config, nextInstanceId++);
            if (!Grid.TryOccupy(config.team, config.position, unit.InstanceId))
                throw new InvalidOperationException("Invalid or occupied grid slot for " + config.id);
            units.Add(unit);
            return unit;
        }

        public void FinishInitialization()
        {
            if (State != BattleState.Init) throw new InvalidOperationException("Battle already initialized.");
            State = BattleState.Preparation;
        }

        public bool MoveUnit(int instanceId, GridPosition destination)
        {
            if (State != BattleState.Preparation) return false;
            var unit = Find(instanceId);
            if (unit == null || Grid.IsOccupied(unit.Config.team, destination)) return false;
            Grid.Vacate(unit.Config.team, unit.Config.position);
            unit.Config.position = destination;
            Grid.TryOccupy(unit.Config.team, destination, instanceId);
            return true;
        }

        public void StartBattle()
        {
            if (State != BattleState.Preparation) throw new InvalidOperationException("Battle is not in preparation.");
            State = BattleState.BattleStart;
            State = BattleState.Fighting;
            EventLog.Add("battle:start");
        }

        public void Tick()
        {
            if (State != BattleState.Fighting) return;
            ElapsedSeconds += TickSeconds;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsAlive) continue;
                if (unit.GroggyTicks > 0) unit.GroggyTicks--;
                unit.SkillCooldown = Math.Max(0f, unit.SkillCooldown - TickSeconds);
                unit.CurrentMp = Math.Min(unit.Config.maxMp, unit.CurrentMp + unit.Config.mpPerTick);
                unit.AttackTimer -= TickSeconds;
            }

            units.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
            for (var i = 0; i < units.Count && State == BattleState.Fighting; i++)
            {
                var actor = units[i];
                if (!actor.IsAlive || actor.GroggyTicks > 0 || actor.AttackTimer > 0f) continue;
                Act(actor);
                actor.AttackTimer += Math.Max(TickSeconds, actor.Config.attackInterval);
            }

            EvaluateEndConditions();
        }

        public CombatUnit SelectAttackTarget(CombatUnit actor)
        {
            CombatUnit best = null;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (!candidate.IsAlive || candidate.Config.team == actor.Config.team) continue;
                var distance = Distance(actor, candidate);
                if (distance > actor.Config.range) continue;
                if (best == null || BetterAttackTarget(candidate, best, actor)) best = candidate;
            }
            return best;
        }

        public CombatUnit SelectHealTarget(CombatUnit actor)
        {
            CombatUnit best = null;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (!candidate.IsAlive || candidate.Config.team != actor.Config.team || candidate.CurrentHp >= candidate.Config.maxHp) continue;
                if (best == null || candidate.HpRatio < best.HpRatio ||
                    (Math.Abs(candidate.HpRatio - best.HpRatio) < 0.0001f && candidate.InstanceId < best.InstanceId))
                    best = candidate;
            }
            return best;
        }

        public bool CanUseManualSkill(int ownerId)
        {
            var owner = Find(ownerId);
            return State == BattleState.Fighting && owner != null && owner.IsAlive && HasLivingBoss() &&
                   owner.SkillCooldown <= 0f && owner.CurrentMp >= owner.Config.maxMp;
        }

        public bool BeginManualSkill(int ownerId)
        {
            if (!CanUseManualSkill(ownerId)) return false;
            selectedSkillOwnerId = ownerId;
            State = BattleState.PlayerSkillSelection;
            return true;
        }

        public bool ResolveManualSkill(int targetId)
        {
            if (State != BattleState.PlayerSkillSelection) return false;
            var owner = Find(selectedSkillOwnerId);
            var target = Find(targetId);
            if (owner == null || target == null || !owner.IsAlive || !target.IsAlive) return false;

            if (owner.Config.role == CombatRole.Healer)
            {
                if (target.Config.team != owner.Config.team) return false;
                ApplyHealing(owner, target, owner.Config.manualSkillMultiplier);
            }
            else
            {
                if (target.Config.team == owner.Config.team) return false;
                ApplyDamage(owner, target, owner.Config.manualSkillMultiplier);
                if (owner.Config.role == CombatRole.Tank && target.Config.isBoss && target.IsAlive)
                    target.GroggyTicks = 20;
            }

            owner.CurrentMp = 0;
            owner.SkillCooldown = owner.Config.manualSkillCooldown;
            selectedSkillOwnerId = -1;
            State = BattleState.Fighting;
            EvaluateEndConditions();
            return true;
        }

        public void CancelManualSkill()
        {
            if (State != BattleState.PlayerSkillSelection) return;
            selectedSkillOwnerId = -1;
            State = BattleState.Fighting;
        }

        public CombatUnit Find(int instanceId)
        {
            for (var i = 0; i < units.Count; i++)
                if (units[i].InstanceId == instanceId) return units[i];
            return null;
        }

        public void ApplyTestDamage(int targetId, int amount)
        {
            var target = Find(targetId);
            if (target != null) target.CurrentHp = Math.Max(0, target.CurrentHp - Math.Max(0, amount));
        }

        public void EvaluateEndConditions()
        {
            if (State != BattleState.Fighting && State != BattleState.PlayerSkillSelection) return;
            var playersAlive = false;
            var enemiesAlive = false;
            for (var i = 0; i < units.Count; i++)
            {
                if (!units[i].IsAlive) continue;
                if (units[i].Config.team == Team.Player) playersAlive = true;
                else enemiesAlive = true;
            }

            if (!enemiesAlive) End(BattleResult.Victory);
            else if (!playersAlive) End(BattleResult.Defeat);
            else if (ElapsedSeconds >= TimeLimitSeconds) End(BattleResult.Timeout);
        }

        private void Act(CombatUnit actor)
        {
            if (actor.Config.role == CombatRole.Healer)
            {
                var ally = SelectHealTarget(actor);
                actor.CurrentTargetId = ally == null ? -1 : ally.InstanceId;
                if (ally != null) ApplyHealing(actor, ally, actor.Config.basicMultiplier);
                return;
            }

            var target = SelectAttackTarget(actor);
            actor.CurrentTargetId = target == null ? -1 : target.InstanceId;
            if (target == null) return;

            if (actor.Config.areaAttack)
            {
                var targets = new List<CombatUnit>();
                for (var i = 0; i < units.Count; i++)
                    if (units[i].IsAlive && units[i].Config.team != actor.Config.team) targets.Add(units[i]);
                for (var i = 0; i < targets.Count; i++) ApplyDamage(actor, targets[i], actor.Config.basicMultiplier);
            }
            else ApplyDamage(actor, target, actor.Config.basicMultiplier);
        }

        private void ApplyDamage(CombatUnit attacker, CombatUnit target, float multiplier)
        {
            var critical = random.NextDouble() <= attacker.Config.critChance;
            var damage = CombatMath.Damage(attacker.Config.attack, multiplier, target.Config.defense, critical);
            if (target.GroggyTicks > 0) damage = Math.Max(1, CombatMath.Damage(damage, 1.5f, 0, false));
            target.CurrentHp = Math.Max(0, target.CurrentHp - damage);
            EventLog.Add("hit:" + attacker.Config.id + ":" + target.Config.id + ":" + damage);
            if (!target.IsAlive) EventLog.Add("death:" + target.Config.id + ":" + ElapsedSeconds.ToString("0.0"));
        }

        private void ApplyHealing(CombatUnit healer, CombatUnit target, float multiplier)
        {
            var amount = CombatMath.Healing(healer.Config.attack, multiplier);
            target.CurrentHp = Math.Min(target.Config.maxHp, target.CurrentHp + amount);
            EventLog.Add("heal:" + healer.Config.id + ":" + target.Config.id + ":" + amount);
        }

        private bool BetterAttackTarget(CombatUnit candidate, CombatUnit current, CombatUnit actor)
        {
            if (candidate.Config.hasTaunt != current.Config.hasTaunt) return candidate.Config.hasTaunt;
            var candidateDistance = Distance(actor, candidate);
            var currentDistance = Distance(actor, current);
            if (candidateDistance != currentDistance) return candidateDistance < currentDistance;
            if (Math.Abs(candidate.HpRatio - current.HpRatio) >= 0.0001f) return candidate.HpRatio < current.HpRatio;
            return candidate.InstanceId < current.InstanceId;
        }

        private static int Distance(CombatUnit first, CombatUnit second)
        {
            if (first.Config.team == second.Config.team)
                return Math.Abs(first.Config.position.row - second.Config.position.row) +
                       Math.Abs(first.Config.position.column - second.Config.position.column);
            return first.Config.position.row + second.Config.position.row + 1 +
                   Math.Abs(first.Config.position.column - second.Config.position.column);
        }

        private bool HasLivingBoss()
        {
            for (var i = 0; i < units.Count; i++)
                if (units[i].IsAlive && units[i].Config.isBoss) return true;
            return false;
        }

        private void End(BattleResult result)
        {
            Result = result;
            State = BattleState.BattleEnd;
            EventLog.Add("battle:end:" + result);
        }
    }
}
