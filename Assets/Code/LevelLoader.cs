using UnityEngine;

/// <summary>
/// Puts the current level's settings into the game scene's systems. It runs first
/// (execution order -1000) so the grid, ships, hazards and music are set up before they start.
/// With no LevelCatalog, nothing changes and the scene plays with its own Inspector values.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class LevelLoader : MonoBehaviour
{
    [Tooltip("Editor only: play this level (1, 2, 3...) when you press Play in this scene. 0 = whatever the menu chose.")]
    [SerializeField] private int editorTestLevel = 0;

    /// <summary>The level being played, or null if there's no catalog.</summary>
    public static LevelData Current { get; private set; }
    public static int CurrentIndex { get; private set; }
    public static int LevelCount { get; private set; }

    private static bool editorTestLevelUsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => editorTestLevelUsed = false;

    void Awake()
    {
        LevelCatalog catalog = LevelCatalog.Load();
        LevelCount = catalog.Count;
        if (LevelCount == 0)
        {
            Current = null;
            CurrentIndex = 0;
            return;
        }

        int index = LevelProgress.CurrentIndex;
        // Only for the first load, so Next Level and Restart still work while testing.
        if (Application.isEditor && editorTestLevel > 0 && !editorTestLevelUsed)
        {
            index = editorTestLevel - 1;
            editorTestLevelUsed = true;
        }
        CurrentIndex = Mathf.Clamp(index, 0, LevelCount - 1);
        LevelProgress.CurrentIndex = CurrentIndex;
        Current = catalog.Get(CurrentIndex);

        Apply(Current);
        Debug.Log($"Level {CurrentIndex + 1}/{LevelCount}: {Current.levelName}");
    }

    private void Apply(LevelData level)
    {
        var grid = FindAnyObjectByType<HexGridSystem>();
        if (grid != null) grid.SetRadius(level.gridRadius);

        var ships = FindAnyObjectByType<ShipManager>();
        if (ships != null) ships.ApplyLevel(level);

        var obstacles = FindAnyObjectByType<ObstacleManager>();
        if (obstacles != null) obstacles.ApplyLevel(level);

        var time = FindAnyObjectByType<TimeManager>();
        if (time != null) time.ApplyLevel(level);

        var game = FindAnyObjectByType<GameManager>();
        if (game != null) game.ApplyLevel(level);

        var letter = FindAnyObjectByType<TutorialLetter>();
        if (letter != null) letter.SetShowAtStart(level.showTutorialLetter);
    }
}
