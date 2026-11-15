using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip audioClip;

    [Header("Playback State")]
    [SerializeField] private string currentNote;
    public string CurrentNote => currentNote;

    void Start()
    {
        if (audioSource != null && audioClip != null)
        {
            audioSource.clip = audioClip;
            audioSource.loop = true; // Enable built-in audio looping
            audioSource.Play();
            StartCoroutine(NoteSequenceRoutine());
        }
        else
        {
            Debug.LogWarning("AudioSource or AudioClip is not assigned.");
        }
    }

    IEnumerator NoteSequenceRoutine()
    {
        float[] thresholds = { 0f, 8f, 16f, 24f, 32f };
        string[] notes = { "C", "F", "G", "C" };

        while (true) // Loop the sequence indefinitely
        {
            for (int i = 0; i < notes.Length; i++)
            {
                SetNote(notes[i]);

                float targetTime = thresholds[i + 1];
                float previousTime = audioSource.time;

                while (audioSource.isPlaying)
                {
                    float currentTime = audioSource.time;

                    // If the audio loops back to 0, time drops. Break out of the wait to reset the sequence.
                    if (currentTime < previousTime || currentTime >= targetTime)
                    {
                        break;
                    }

                    previousTime = currentTime;
                    yield return null;
                }

                if (!audioSource.isPlaying) yield break;
            }
        }
    }

    void SetNote(string note)
    {
        currentNote = note;
        Debug.Log($"Current Note: {currentNote} at time: {audioSource.time:F2}s");
    }
}