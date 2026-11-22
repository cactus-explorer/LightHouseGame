using System;
using UnityEngine;

/// <summary>
/// One place that knows whether the game is paused (pause menu, tutorial letter).
/// Pausing stops time, the music and therefore the turn timer, and blocks note input.
/// </summary>
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    private static int resumedFrame = -1;

    /// <summary>
    /// True on the frame the game unpaused, so the key that closed a menu
    /// (Enter, Space, Esc) isn't also read as gameplay input.
    /// </summary>
    public static bool JustResumed => Time.frameCount == resumedFrame;

    /// <summary>Paused, or unpaused this very frame: gameplay input should be ignored.</summary>
    public static bool BlocksInput => IsPaused || JustResumed;

    /// <summary>Fired with the new paused state.</summary>
    public static event Action<bool> OnPauseChanged;

    public static void Set(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        if (!paused) resumedFrame = Time.frameCount;
        Time.timeScale = paused ? 0f : 1f;
        OnPauseChanged?.Invoke(paused);
    }

    // Statics survive between plays when domain reload is off, so reset when Play mode starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        IsPaused = false;
        OnPauseChanged = null;
        Time.timeScale = 1f;
    }

    /// <summary>Call before loading another scene so time doesn't stay frozen.</summary>
    public static void Clear()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}
