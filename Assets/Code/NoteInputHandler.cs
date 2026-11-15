using UnityEngine;
using UnityEngine.InputSystem;

public class NoteInputHandler : MonoBehaviour
{
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private ShipSpawner shipSpawner;

    [Header("Audio Clips per Note (Size 5: A, S, D, F, Space)")]
    [SerializeField] private AudioClip[] clipsC = new AudioClip[5];
    [SerializeField] private AudioClip[] clipsF = new AudioClip[5];
    [SerializeField] private AudioClip[] clipsG = new AudioClip[5];

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        int keyIndex = -1;
        string keyName = string.Empty;

        // Only use the 5 note keys: A, S, D, F, and Space (with overlapping ship controls)
        if (keyboard.aKey.wasPressedThisFrame)
        {
            keyIndex = 0;
            keyName = "A";
            if (shipSpawner != null)
            {
                shipSpawner.MoveShipTypeUp("DinghyA");
                shipSpawner.MoveShipTypeUp("GalleonA");
            }
        }
        else if (keyboard.sKey.wasPressedThisFrame)
        {
            keyIndex = 1;
            keyName = "S";
            if (shipSpawner != null)
            {
                shipSpawner.MoveShipTypeDown("DinghyA");
                shipSpawner.MoveShipTypeUp("SloopB");
            }
        }
        else if (keyboard.dKey.wasPressedThisFrame)
        {
            keyIndex = 2;
            keyName = "D";
            if (shipSpawner != null)
            {
                shipSpawner.MoveShipTypeDown("DinghyB");
                shipSpawner.MoveShipTypeDown("SloopB");
                shipSpawner.MoveShipTypeUp("GalleonB");
            }
        }
        else if (keyboard.fKey.wasPressedThisFrame)
        {
            keyIndex = 3;
            keyName = "F";
            if (shipSpawner != null)
            {
                shipSpawner.MoveShipTypeUp("SloopA");
                shipSpawner.MoveShipTypeDown("GalleonA");
            }
        }
        else if (keyboard.spaceKey.wasPressedThisFrame)
        {
            keyIndex = 4;
            keyName = "Space";
            if (shipSpawner != null)
            {
                shipSpawner.MoveShipTypeUp("DinghyB");
                shipSpawner.MoveShipTypeDown("SloopA");
                shipSpawner.MoveShipTypeDown("GalleonB");
            }
        }

        if (keyIndex != -1)
        {
            Debug.Log($"Input detected: {keyName} (Index: {keyIndex})");
            TriggerSoundForCurrentNote(keyIndex);
        }
    }

    void TriggerSoundForCurrentNote(int index)
    {
        if (timeManager == null || sfxAudioSource == null)
        {
            Debug.LogWarning("TimeManager or AudioSource reference is missing.");
            return;
        }

        string activeNote = timeManager.CurrentNote;
        AudioClip[] targetArray = null;

        switch (activeNote)
        {
            case "C":
                targetArray = clipsC;
                break;
            case "F":
                targetArray = clipsF;
                break;
            case "G":
                targetArray = clipsG;
                break;
            default:
                Debug.Log($"No active note or unhandled note state ('{activeNote}').");
                break;
        }

        if (targetArray != null)
        {
            if (index >= 0 && index < targetArray.Length)
            {
                AudioClip targetClip = targetArray[index];
                if (targetClip != null)
                {
                    sfxAudioSource.PlayOneShot(targetClip);
                    Debug.Log($"Played sound for note: {activeNote} at array index {index}");
                }
                else
                {
                    Debug.LogWarning($"AudioClip at index {index} for note {activeNote} is unassigned.");
                }
            }
        }
    }
}
