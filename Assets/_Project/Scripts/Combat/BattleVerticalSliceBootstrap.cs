using System.Collections.Generic;
using UnityEngine;

namespace AIUnbound.Combat
{
    public sealed class BattleVerticalSliceBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Application.targetFrameRate = 60;
            var battle = gameObject.AddComponent<BattleManager>();
            var grid = gameObject.AddComponent<GridManager>();
            battle.Initialize(CreateStage(), 13579, 45f);
            gameObject.AddComponent<BattleUIController>().Build(battle, grid);
        }

        public static List<CombatUnitConfig> CreateStage()
        {
            return new List<CombatUnitConfig>
            {
                Unit("aegis", "Aegis", Team.Player, CombatRole.Tank, 900, 80, 42, 1.1f, 0, 0, true),
                Unit("striker", "Striker", Team.Player, CombatRole.Damage, 620, 135, 20, 0.8f, 1, 1),
                Unit("pulse", "Pulse", Team.Player, CombatRole.Healer, 580, 72, 18, 1.2f, 2, 2),
                Unit("drone", "Drone", Team.Enemy, CombatRole.Damage, 480, 82, 18, 1.1f, 0, 0),
                Unit("turret", "Turret", Team.Enemy, CombatRole.Damage, 430, 102, 16, 1.3f, 1, 2),
                Unit("overseer", "Overseer", Team.Enemy, CombatRole.Boss, 1500, 82, 30, 3f, 2, 1, false, true, true)
            };
        }

        private static CombatUnitConfig Unit(string id, string name, Team team, CombatRole role, int hp, int attack,
            int defense, float attackInterval, int row, int column, bool taunt = false, bool boss = false, bool area = false)
        {
            return new CombatUnitConfig
            {
                id = id,
                displayName = name,
                team = team,
                role = role,
                maxHp = hp,
                attack = attack,
                defense = defense,
                attackInterval = attackInterval,
                range = 9,
                basicMultiplier = role == CombatRole.Healer ? 1.1f : 1f,
                critChance = role == CombatRole.Damage ? 0.1f : 0f,
                manualSkillMultiplier = role == CombatRole.Healer ? 2.5f : role == CombatRole.Damage ? 3f : 1.5f,
                manualSkillCooldown = 6f,
                hasTaunt = taunt,
                isBoss = boss,
                areaAttack = area,
                position = new GridPosition(row, column)
            };
        }
    }
}
