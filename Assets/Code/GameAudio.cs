using UnityEngine;

/// <summary>
/// Plays sound effects for gameplay events. Assign whichever clips exist; empty slots are skipped.
/// The note/bell sounds stay in NoteInputHandler.
/// </summary>
public class GameAudio : MonoBehaviour
{
    [SerializeField] private ShipManager shipManager;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ObstacleManager obstacleManager;

    [Header("Sources (created automatically if empty)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource stormAmbienceSource;

    [Header("Clips")]
    [Tooltip("Water slosh when ships coast forward a tile. Plays once per turn.")]
    [SerializeField] private AudioClip moveSlosh;
    [Tooltip("Ship hits a rock or another ship.")]
    [SerializeField] private AudioClip crack;
    [SerializeField] private AudioClip shipwreck;
    [Tooltip("Ship sails off the right edge safely.")]
    [SerializeField] private AudioClip shipSaved;
    [Tooltip("Gear shift / mechanical click as the bells swap to the new key.")]
    [SerializeField] private AudioClip chordChangeClick;
    [Tooltip("Ticks once a second in the last few seconds of a turn.")]
    [SerializeField] private AudioClip turnWarningTick;
    [SerializeField] private float warningSeconds = 2f;
    [SerializeField] private AudioClip levelWon;
    [SerializeField] private AudioClip levelLost;
    [Tooltip("Looping waves and seagulls.")]
    [SerializeField] private AudioClip seaAmbience;

    [Header("Hazard Clips")]
    [SerializeField] private AudioClip monsterAppears;
    [SerializeField] private AudioClip monsterRoar;
    [Tooltip("Plays when grey storm clouds first appear (the warning turn).")]
    [SerializeField] private AudioClip stormWarning;
    [SerializeField] private AudioClip thunder;
    [Tooltip("Looping rain/wind, faded in while any storm is active.")]
    [SerializeField] private AudioClip stormAmbience;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.4f;

    void Start()
    {
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (obstacleManager == null) obstacleManager = FindAnyObjectByType<ObstacleManager>();

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        if (seaAmbience != null)
        {
            if (ambienceSource == null) ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.clip = seaAmbience;
            ambienceSource.loop = true;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.Play();
        }

        if (stormAmbience != null)
        {
            if (stormAmbienceSource == null) stormAmbienceSource = gameObject.AddComponent<AudioSource>();
            stormAmbienceSource.clip = stormAmbience;
            stormAmbienceSource.loop = true;
            stormAmbienceSource.volume = 0f;
            stormAmbienceSource.Play();
        }

        if (obstacleManager != null)
        {
            obstacleManager.OnMonsterAppeared += HandleMonsterAppeared;
            obstacleManager.OnMonsterAttack += HandleMonsterAttack;
            obstacleManager.OnStormWarning += HandleStormWarning;
            obstacleManager.OnStormStrike += HandleStormStrike;
        }

        if (shipManager != null)
        {
            shipManager.OnTurnAdvanced += HandleTurn;
            shipManager.OnShipDamaged += HandleDamaged;
            shipManager.OnShipWrecked += HandleWrecked;
            shipManager.OnShipSaved += HandleSaved;
        }
        if (timeManager != null) timeManager.OnChordChanged += HandleChord;
        if (gameManager != null) gameManager.OnGameOver += HandleGameOver;
    }

    private int lastTickSecond = -1;

    void Update()
    {
        UpdateWarningTick();

        // Fade the storm loop in while a storm is active, out when it clears.
        if (stormAmbienceSource == null || obstacleManager == null) return;
        float target = obstacleManager.AnyStormActive ? ambienceVolume : 0f;
        stormAmbienceSource.volume = Mathf.MoveTowards(stormAmbienceSource.volume, target, Time.deltaTime * 0.5f);
    }

    void OnDestroy()
    {
        if (obstacleManager != null)
        {
            obstacleManager.OnMonsterAppeared -= HandleMonsterAppeared;
            obstacleManager.OnMonsterAttack -= HandleMonsterAttack;
            obstacleManager.OnStormWarning -= HandleStormWarning;
            obstacleManager.OnStormStrike -= HandleStormStrike;
        }
        if (shipManager != null)
        {
            shipManager.OnTurnAdvanced -= HandleTurn;
            shipManager.OnShipDamaged -= HandleDamaged;
            shipManager.OnShipWrecked -= HandleWrecked;
            shipManager.OnShipSaved -= HandleSaved;
        }
        if (timeManager != null) timeManager.OnChordChanged -= HandleChord;
        if (gameManager != null) gameManager.OnGameOver -= HandleGameOver;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private void HandleTurn(int turn)
    {
        if (shipManager.ActiveShipCount > 0) Play(moveSlosh);
    }

    private void UpdateWarningTick()
    {
        if (turnWarningTick == null || timeManager == null || !timeManager.IsPlaying) return;
        if (shipManager != null && (!shipManager.IsRunning || shipManager.ActiveShipCount == 0)) return;

        float left = timeManager.TimeUntilNextChord;
        int second = Mathf.CeilToInt(left);
        if (left <= warningSeconds && second != lastTickSecond && second > 0)
        {
            Play(turnWarningTick);
        }
        lastTickSecond = second;
    }

    private void HandleDamaged(Vector3 _) => Play(crack);
    private void HandleWrecked(Vector3 _) => Play(shipwreck);
    private void HandleSaved(Vector3 _) => Play(shipSaved);
    private void HandleChord(int index, string chord) => Play(chordChangeClick);
    private void HandleGameOver(bool won) => Play(won ? levelWon : levelLost);
    private void HandleMonsterAppeared(Vector3 _) => Play(monsterAppears);
    private void HandleMonsterAttack(Vector3 _) => Play(monsterRoar);
    private void HandleStormWarning(Vector3 _) => Play(stormWarning);
    private void HandleStormStrike(Vector3 _) => Play(thunder);
}
