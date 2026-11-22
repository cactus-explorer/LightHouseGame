using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The 5 note keys (A, S, D, F, Space). Each press plays the note for the current chord
/// and steers the ships bound to that key (see ShipControls).
/// </summary>
public class NoteInputHandler : MonoBehaviour
{
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private ShipManager shipManager;

    [Header("Audio Clips per Note (Size 5: A, S, D, F, Space)")]
    [SerializeField] private AudioClip[] clipsC = new AudioClip[5];
    [SerializeField] private AudioClip[] clipsF = new AudioClip[5];
    [SerializeField] private AudioClip[] clipsG = new AudioClip[5];

    /// <summary>Fired with the key index (0-4) on every note press. The HUD uses it to flash the key.</summary>
    public static event Action<int> OnNoteKeyPressed;

    private static readonly Key[] noteKeys = { Key.A, Key.S, Key.D, Key.F, Key.Space };

    void Start()
    {
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
    }

    void Update()
    {
        if (GamePause.BlocksInput) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i < noteKeys.Length; i++)
        {
            if (!keyboard[noteKeys[i]].wasPressedThisFrame) continue;

            if (shipManager != null)
            {
                foreach (var (type, direction) in ShipControls.CommandsForKey(i))
                {
                    shipManager.QueueMove(type, direction);
                }
            }

            OnNoteKeyPressed?.Invoke(i);
            TriggerSoundForCurrentNote(i);
            break; // one note per frame, as before
        }
    }

    void TriggerSoundForCurrentNote(int index)
    {
        if (timeManager == null || sfxAudioSource == null)
        {
            Debug.LogWarning("NoteInputHandler: TimeManager or AudioSource reference is missing.");
            return;
        }

        AudioClip[] targetArray;
        switch (timeManager.CurrentNote)
        {
            case "C": targetArray = clipsC; break;
            case "F": targetArray = clipsF; break;
            case "G": targetArray = clipsG; break;
            default:
                Debug.Log($"No clips for chord '{timeManager.CurrentNote}'.");
                return;
        }

        if (targetArray == null || index < 0 || index >= targetArray.Length) return;

        AudioClip clip = targetArray[index];
        if (clip != null) sfxAudioSource.PlayOneShot(clip);
        else Debug.LogWarning($"AudioClip at index {index} for chord {timeManager.CurrentNote} is unassigned.");
    }
}
