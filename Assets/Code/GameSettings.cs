using UnityEngine;

/// <summary>Player settings saved between sessions. Only master volume for now.</summary>
public static class GameSettings
{
    private const string VolumeKey = "MasterVolume";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, v);
            PlayerPrefs.Save();
            AudioListener.volume = v;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply() => AudioListener.volume = MasterVolume;
}
