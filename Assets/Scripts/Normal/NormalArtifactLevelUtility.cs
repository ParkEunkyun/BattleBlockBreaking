using UnityEngine;

public static class NormalArtifactLevelUtility
{
    private const int MinLevel = 1;
    private const int MaxLevel = 10;
    private const string LevelKeyPrefix = "BBB_NORMAL_ARTIFACT_LEVEL_";

    public static int GetLevel(NormalArtifactDefinition def)
    {
        if (def == null)
            return MinLevel;

        return GetLevel(def.artifactId);
    }

    public static int GetLevel(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return MinLevel;

        return Mathf.Clamp(PlayerPrefs.GetInt(LevelKeyPrefix + artifactId, MinLevel), MinLevel, MaxLevel);
    }

    public static void SetLevel(NormalArtifactDefinition def, int level, bool saveImmediately = true)
    {
        if (def == null)
            return;

        SetLevel(def.artifactId, level, saveImmediately);
    }

    public static void SetLevel(string artifactId, int level, bool saveImmediately = true)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return;

        PlayerPrefs.SetInt(LevelKeyPrefix + artifactId, Mathf.Clamp(level, MinLevel, MaxLevel));

        if (saveImmediately)
            PlayerPrefs.Save();
    }
}