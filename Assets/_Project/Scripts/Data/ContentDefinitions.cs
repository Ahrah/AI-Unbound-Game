using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIUnbound.Data
{
    [Serializable]
    public struct CharacterStats
    {
        public int hp;
        public int attack;
        public int defense;
    }

    [Serializable]
    public struct ActiveSkillData
    {
        public string skillId;
        public string displayNameKey;
        public float cooldownSeconds;
        public int value;
        public float durationSeconds;
        public bool bossControl;
    }

    [CreateAssetMenu(menuName = "AI Unbound/Character Definition", fileName = "Character_")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string characterId;
        public string displayNameKey;
        public CharacterRole role;
        public CharacterGrade grade;
        public string obtainKey;
        public bool isPaid;

        [Header("Progression")]
        [Min(1)] public int maxLevel = 30;
        public CharacterStats baseStats;
        public CharacterStats growthPerLevel;
        public ActiveSkillData activeSkill;

        private void OnValidate()
        {
            maxLevel = Mathf.Max(1, maxLevel);
            activeSkill.cooldownSeconds = Mathf.Max(0f, activeSkill.cooldownSeconds);
            activeSkill.durationSeconds = Mathf.Max(0f, activeSkill.durationSeconds);
        }
    }

    [CreateAssetMenu(menuName = "AI Unbound/Stage Definition", fileName = "Stage_")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stageId;
        public int chapter;
        public BranchId branch = BranchId.None;
        public string previousStageId;
        public bool isBoss;

        [Header("Rules")]
        [Min(1)] public int requiredEnergy = GameDataDefaults.StageEnergyCost;
        [Min(1f)] public float timeLimitSeconds = 90f;
        public SpecialRuleType specialRule;
        public float specialRuleValue;

        [Header("Authored content")]
        public List<WaveData> waves = new List<WaveData>();
        public List<RewardData> firstClearRewards = new List<RewardData>();
        public List<RewardData> repeatRewards = new List<RewardData>();

        private void OnValidate()
        {
            requiredEnergy = Mathf.Max(1, requiredEnergy);
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
            specialRuleValue = Mathf.Max(0f, specialRuleValue);
        }
    }
}
