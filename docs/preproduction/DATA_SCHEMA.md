[Serializable]
public struct CharacterData {
    public string id;
    public int level;
    public int skillTier;
    // Current stats calculated dynamically: Base + (Growth * Level)
}

[Serializable]
public struct UserProfile {
    public string uid;
    public int dataCoin;
    public int quantumCore;
    public int energy;
    public long lastEnergyUpdateTime;
    public List<CharacterData> unlockedCharacters;
    public int highestStageCleared;
    public string selectedBranch; // "None", "Coexist", "Independent", "Dominate"
}

[Serializable]
public struct StageConfig {
    public string stageId;
    public int requiredEnergy;
    public WaveData[] waves;
    public RewardData firstClearReward;
}
