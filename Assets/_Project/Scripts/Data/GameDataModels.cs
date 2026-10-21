using System;
using System.Collections.Generic;

namespace AIUnbound.Data
{
    public static class GameDataDefaults
    {
        public const int SaveVersion = 1;
        public const int MaxEnergy = 50;
        public const int EnergyRechargeMinutes = 10;
        public const int StageEnergyCost = 10;
    }

    public enum CharacterRole { Tank, Healer, Dps, Control, Support, Mage, Special, Commander }
    public enum CharacterGrade { B, A, S }
    public enum BranchId { None, Coexist, Independent, Dominate }
    public enum RewardType { DataCoin, QuantumCore, MemoryChip, Character, Ending }
    public enum SpecialRuleType { None, EnemyAttackUp, AllyDefenseUp, AllyAttackUp, NoHeal, BossShield, BossSummon, BossWipeout }

    [Serializable]
    public struct CharacterProgress
    {
        public string characterId;
        public int level;
        public int skillTier;

        public CharacterProgress(string id, int initialLevel = 1)
        {
            characterId = id;
            level = initialLevel;
            skillTier = 0;
        }
    }

    [Serializable]
    public struct ClearedStage
    {
        public string stageId;
        public int stars;
        public long clearedAtUnixMs;
    }

    [Serializable]
    public struct CurrencyBalance
    {
        public int dataCoin;
        public int quantumCore;
        public int memoryChip;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int saveVersion = GameDataDefaults.SaveVersion;
        public string uid;
        public long createdAtUnixMs;
        public long updatedAtUnixMs;
        public int energy = GameDataDefaults.MaxEnergy;
        public long lastEnergyServerUnixMs;
        public CurrencyBalance currencies;
        public BranchId selectedBranch = BranchId.None;
        public List<CharacterProgress> unlockedCharacters = new List<CharacterProgress>();
        public List<ClearedStage> clearedStages = new List<ClearedStage>();

        public bool HasCleared(string stageId)
        {
            for (var i = 0; i < clearedStages.Count; i++)
                if (clearedStages[i].stageId == stageId) return true;
            return false;
        }

        public bool HasCharacter(string characterId)
        {
            for (var i = 0; i < unlockedCharacters.Count; i++)
                if (unlockedCharacters[i].characterId == characterId) return true;
            return false;
        }
    }

    [Serializable]
    public struct RewardData
    {
        public RewardType type;
        public string itemId;
        public int amount;
    }

    [Serializable]
    public struct EnemySpawnData
    {
        public string enemyId;
        public int count;
        public int gridColumn;
        public int gridRow;
    }

    [Serializable]
    public struct WaveData
    {
        public float startTimeSeconds;
        public List<EnemySpawnData> enemies;
    }
}
