using UnityEngine;

/// <summary>
/// The ordered list of levels. Lives at Assets/Resources/LevelCatalog.asset so both
/// the menu and the game scene can find it without any references set up.
/// </summary>
[CreateAssetMenu(menuName = "Lighthouse/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    public const string ResourcePath = "LevelCatalog";

    public LevelData[] levels;

    private static LevelCatalog cached;

    public int Count => levels != null ? levels.Length : 0;

    public LevelData Get(int index) =>
        Count == 0 ? null : levels[Mathf.Clamp(index, 0, Count - 1)];

    /// <summary>The catalog from Resources, or an empty one (the game then runs with the scene's own settings).</summary>
    public static LevelCatalog Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<LevelCatalog>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning("No LevelCatalog found in Assets/Resources. Using the scene's own settings as a single level.");
            cached = CreateInstance<LevelCatalog>();
            cached.levels = new LevelData[0];
        }
        return cached;
    }
}
