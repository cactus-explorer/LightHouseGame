using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Win/lose rules for the level: save enough ships to win, wreck too many and you lose.
/// Press R after the level ends to restart.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Playing, Won, Lost }

    [SerializeField] private ShipManager shipManager;

    [Header("Level Rules")]
    [SerializeField, Min(1)] private int shipsToSave = 10;
    [SerializeField, Min(1)] private int maxWrecks = 3;

    /// <summary>True if the player won.</summary>
    public event Action<bool> OnGameOver;

    public State CurrentState { get; private set; } = State.Playing;
    public int ShipsToSave => shipsToSave;
    public int MaxWrecks => maxWrecks;
    public int Saved => shipManager != null ? shipManager.SavedCount : 0;
    public int Wrecked => shipManager != null ? shipManager.WreckedCount : 0;

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
    }

    void OnDestroy()
    {
        if (shipManager == null) return;
        shipManager.OnShipSaved -= HandleShipEvent;
        shipManager.OnShipWrecked -= HandleShipEvent;
    }

    void Update()
    {
        if (CurrentState == State.Playing) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
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
        Debug.Log(won ? "Level won!" : "Level lost.");
        OnGameOver?.Invoke(won);
    }

    public void Restart()
    {
        // The scene has to be in the build's scene list for this to work.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
