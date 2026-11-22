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

    [Header("Sources (created automatically if empty)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource ambienceSource;

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
    [SerializeField] private AudioClip levelWon;
    [SerializeField] private AudioClip levelLost;
    [Tooltip("Looping waves and seagulls.")]
    [SerializeField] private AudioClip seaAmbience;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.4f;

    void Start()
    {
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

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

    void OnDestroy()
    {
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

    private void HandleDamaged(Vector3 _) => Play(crack);
    private void HandleWrecked(Vector3 _) => Play(shipwreck);
    private void HandleSaved(Vector3 _) => Play(shipSaved);
    private void HandleChord(int index, string chord) => Play(chordChangeClick);
    private void HandleGameOver(bool won) => Play(won ? levelWon : levelLost);
}
