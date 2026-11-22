using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Win/lose rules for the level: save enough ships to win, wreck too many and you lose.
/// After the level ends: R restarts, N goes to the next level (after a win), M goes back to the menu.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Playing, Won, Lost }

    [SerializeField] private ShipManager shipManager;
    [Tooltip("Scene loaded by M after the level ends. Leave empty if there's no menu scene.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Level Rules")]
    [SerializeField, Min(1)] private int shipsToSave = 10;
    [SerializeField, Min(1)] private int maxWrecks = 3;

    /// <summary>True if the player won.</summary>
    public event Action<bool> OnGameOver;

    public State CurrentState { get; private set; } = State.Playing;
    public bool HasMenu => !string.IsNullOrEmpty(menuSceneName);
    public int ShipsToSave => shipsToSave;
    public int MaxWrecks => maxWrecks;
    public int Saved => shipManager != null ? shipManager.SavedCount : 0;
    public int Wrecked => shipManager != null ? shipManager.WreckedCount : 0;
    public int HitsTaken { get; private set; }
    public int TurnsTaken => shipManager != null ? shipManager.TurnNumber : 0;

    /// <summary>0 if lost. On a win: 3 stars with no wrecks, 2 with one, otherwise 1.</summary>
    public int Stars => CurrentState != State.Won ? 0 : Wrecked == 0 ? 3 : Wrecked == 1 ? 2 : 1;

    /// <summary>Best star rating ever reached on this level (saved between sessions).</summary>
    public int BestStars => LevelProgress.GetStars(LevelLoader.CurrentIndex);
    public bool NewBest { get; private set; }

    public string LevelName => LevelLoader.Current != null ? LevelLoader.Current.levelName : "";
    public int LevelNumber => LevelLoader.CurrentIndex + 1;
    public int LevelCount => Mathf.Max(1, LevelLoader.LevelCount);
    public bool HasNextLevel => LevelLoader.CurrentIndex + 1 < LevelLoader.LevelCount;

    /// <summary>Use a level's goal. Called by LevelLoader before Start.</summary>
    public void ApplyLevel(LevelData level)
    {
        shipsToSave = Mathf.Max(1, level.shipsToSave);
        maxWrecks = Mathf.Max(1, level.maxWrecks);
    }

    void Start()
    {
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (shipManager == null)
        {
            Debug.LogError("GameManager: no ShipManager in the scene.");
            return;
        }
        shipManager.OnShipSaved += HandleShipEvent;
        shipManager.OnShipWrecked += HandleShipEvent;
        shipManager.OnShipDamaged += HandleDamaged;
    }

    void OnDestroy()
    {
        if (shipManager == null) return;
        shipManager.OnShipSaved -= HandleShipEvent;
        shipManager.OnShipWrecked -= HandleShipEvent;
        shipManager.OnShipDamaged -= HandleDamaged;
    }

    private void HandleDamaged(Vector3 _)
    {
        if (CurrentState == State.Playing) HitsTaken++;
    }

    void Update()
    {
        if (CurrentState == State.Playing) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.rKey.wasPressedThisFrame) Restart();
        else if (CurrentState == State.Won && HasNextLevel && keyboard.nKey.wasPressedThisFrame) LoadNextLevel();
        else if (HasMenu && keyboard.mKey.wasPressedThisFrame) ReturnToMenu();
    }

    private void HandleShipEvent(Vector3 _)
    {
        if (CurrentState != State.Playing) return;
        if (Saved >= shipsToSave) EndLevel(true);
        else if (Wrecked >= maxWrecks) EndLevel(false);
    }

    private void EndLevel(bool won)
    {
        CurrentState = won ? State.Won : State.Lost;
        shipManager.SetRunning(false);

        if (won)
        {
            int previous = BestStars;
            if (LevelProgress.RecordStars(LevelLoader.CurrentIndex, Stars))
                NewBest = previous > 0; // a first-ever win isn't a "new best", just a result
            LevelProgress.UnlockUpTo(LevelLoader.CurrentIndex + 2);
        }
        Debug.Log(won ? "Level won!" : "Level lost.");
        OnGameOver?.Invoke(won);
    }

    public void Restart()
    {
        // The scene has to be in the build's scene list for this to work.
        GamePause.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        if (!HasNextLevel) return;
        LevelProgress.CurrentIndex = LevelLoader.CurrentIndex + 1;
        Restart(); // same scene, next level's settings
    }

    public void ReturnToMenu()
    {
        if (!HasMenu) return;
        GamePause.Clear();
        SceneManager.LoadScene(menuSceneName);
    }
}
