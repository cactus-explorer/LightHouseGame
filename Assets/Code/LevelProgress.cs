using UnityEngine;

/// <summary>
/// Which level to play next, which levels are unlocked, and the best stars per level.
/// The current level survives scene loads (static); unlocks and stars are saved in PlayerPrefs.
/// </summary>
public static class LevelProgress
{
    private const string UnlockedKey = "LevelsUnlocked";

    /// <summary>0-based index of the level the game scene should load.</summary>
    public static int CurrentIndex { get; set; }

    /// <summary>How many levels are playable (always at least 1).</summary>
    public static int UnlockedCount => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));

    public static bool IsUnlocked(int index) => index < UnlockedCount;

    public static void UnlockUpTo(int count)
    {
        if (count <= UnlockedCount) return;
        PlayerPrefs.SetInt(UnlockedKey, count);
        PlayerPrefs.Save();
    }

    public static int GetStars(int index) => PlayerPrefs.GetInt($"LevelStars_{index}", 0);

    /// <summary>Saves the rating if it beats the old one. Returns true if it did.</summary>
    public static bool RecordStars(int index, int stars)
    {
        if (stars <= GetStars(index)) return false;
        PlayerPrefs.SetInt($"LevelStars_{index}", stars);
        PlayerPrefs.Save();
        return true;
    }

    public static void ResetAll()
    {
        int count = Mathf.Max(UnlockedCount, 16);
        PlayerPrefs.DeleteKey(UnlockedKey);
        for (int i = 0; i < count; i++) PlayerPrefs.DeleteKey($"LevelStars_{i}");
        PlayerPrefs.Save();
        CurrentIndex = 0;
    }
}
