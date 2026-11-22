using System;
using UnityEngine;

/// <summary>
/// Plays the looping background track and works out which chord is playing.
/// Fires OnChordChanged at each chord boundary; ShipManager uses it as the turn timer.
/// </summary>
public class TimeManager : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip audioClip;

    [Header("Chord Layout")]
    [Tooltip("Chords in the order they play. The default matches the 32 s test track.")]
    [SerializeField] private string[] chordNames = { "C", "F", "G", "C" };
    [Tooltip("Seconds per chord. Also the length of one turn.")]
    [SerializeField] private float chordDuration = 8f;

    [Header("Playback State (read only)")]
    [SerializeField] private string currentNote;
    [SerializeField] private int currentChordIndex = -1;

    /// <summary>Chord index (0-based) and chord name. Fired on every change, including the loop back to the start.</summary>
    public event Action<int, string> OnChordChanged;

    public string CurrentNote => currentNote;
    public int CurrentChordIndex => currentChordIndex;
    public float ChordDuration => chordDuration;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsPaused { get; private set; }

    /// <summary>0 at the start of the chord, 1 just before the next one.</summary>
    public float ChordProgress
    {
        get
        {
            if (!IsPlaying || chordDuration <= 0f) return 0f;
            return Mathf.Clamp01((PlaybackTime % chordDuration) / chordDuration);
        }
    }

    public float TimeUntilNextChord => chordDuration * (1f - ChordProgress);

    // timeSamples is more precise than time for long clips.
    private float PlaybackTime =>
        audioSource.clip != null && audioSource.clip.frequency > 0
            ? (float)audioSource.timeSamples / audioSource.clip.frequency
            : audioSource.time;

    void Start()
    {
        if (audioSource == null || audioClip == null)
        {
            Debug.LogWarning("TimeManager: AudioSource or AudioClip is not assigned.");
            return;
        }

        GamePause.OnPauseChanged += HandlePause;

        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
        if (GamePause.IsPaused) HandlePause(true); // e.g. the tutorial letter opened first

        // Set the first chord without firing the event: the first turn shouldn't end instantly.
        SetChord(IndexAt(0f), fireEvent: false);
    }

    void OnDestroy()
    {
        GamePause.OnPauseChanged -= HandlePause;
    }

    /// <summary>Pausing the music pauses the turn timer too, since turns follow the music.</summary>
    private void HandlePause(bool paused)
    {
        if (audioSource == null) return;
        IsPaused = paused;
        if (paused) audioSource.Pause();
        else audioSource.UnPause();
    }

    void Update()
    {
        if (!IsPlaying || chordNames.Length == 0) return;

        int index = IndexAt(PlaybackTime);
        if (index != currentChordIndex) SetChord(index, fireEvent: true);
    }

    private int IndexAt(float time)
    {
        if (chordNames.Length == 0 || chordDuration <= 0f) return 0;
        return Mathf.Clamp(Mathf.FloorToInt(time / chordDuration), 0, chordNames.Length - 1);
    }

    private void SetChord(int index, bool fireEvent)
    {
        currentChordIndex = index;
        currentNote = chordNames.Length > 0 ? chordNames[index] : string.Empty;
        Debug.Log($"Current Note: {currentNote} at time: {audioSource.time:F2}s");
        if (fireEvent) OnChordChanged?.Invoke(index, currentNote);
    }
}
