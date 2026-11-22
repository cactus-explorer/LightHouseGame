using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup, under the "Lighthouse" menu in the editor's top bar.
///
/// Set Up Game Scene: run it with the gameplay scene open. It
///  - removes "missing script" components (left behind when .meta files weren't shared)
///  - adds any of the game's components that are missing, on the GameManager object
///  - fills empty slots by asset name: bgm, Piano{C,F,G}_{1-5} note clips, ship prefabs named after their type
///  - adds CameraRig to the camera
/// It never overwrites a slot that's already filled, so it's safe to run again.
///
/// Create Main Menu Scene: makes Assets/Scenes/MainMenu.unity and puts it first in the build's scene list.
/// </summary>
public static class LighthouseSetup
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string DefaultMusicName = "DynamicMusicTestTrack";

    [MenuItem("Lighthouse/Set Up Game Scene")]
    public static void SetUpGameScene()
    {
        var log = new List<string>();
        Undo.SetCurrentGroupName("Lighthouse: Set Up Game Scene");
        int undoGroup = Undo.GetCurrentGroup();

        RemoveMissingScripts(log);

        GameObject host = GameObject.Find("GameManager");
        if (host == null)
        {
            host = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(host, "Create GameManager");
            log.Add("Created a GameManager object.");
        }

        var grid = Ensure<HexGridSystem>(host, log);
        var time = Ensure<TimeManager>(host, log);
        var notes = Ensure<NoteInputHandler>(host, log);
        var ships = Ensure<ShipManager>(host, log);
        Ensure<ObstacleManager>(host, log);
        Ensure<GameManager>(host, log);
        Ensure<GameHUD>(host, log);
        Ensure<GameAudio>(host, log);
        Ensure<PauseMenu>(host, log);
        Ensure<TutorialLetter>(host, log);
        Ensure<MapBackground>(host, log);
        Ensure<FeedbackFX>(host, log);

        // The bell tower gets its own object, since it's placed in the world.
        if (Object.FindAnyObjectByType<BellTower>(FindObjectsInactive.Include) == null)
        {
            var towerObj = new GameObject("BellTower");
            Undo.RegisterCreatedObjectUndo(towerObj, "Create BellTower");
            Undo.AddComponent<BellTower>(towerObj);
            log.Add("Added a BellTower object.");
        }

        AssignAudio(host, time, notes, log);
        AssignShipPrefabs(ships, log);

        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam != null && cam.GetComponent<CameraRig>() == null)
        {
            Undo.AddComponent<CameraRig>(cam.gameObject);
            log.Add($"Added CameraRig to {cam.name}.");
        }
        else if (cam == null)
        {
            log.Add("No camera in the scene: add one, then run this again to add CameraRig.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(log.Count == 0
            ? "Lighthouse setup: everything was already in place."
            : "Lighthouse setup:\n- " + string.Join("\n- ", log) + "\n(Save the scene to keep these changes.)");
    }

    [MenuItem("Lighthouse/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        if (File.Exists(MenuScenePath))
        {
            Debug.Log($"Lighthouse: {MenuScenePath} already exists. Fixing the build scene list only.");
            FixBuildSceneList();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string gameScenePath = FindGameScenePath();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var menuObj = new GameObject("Menu");
        var menu = menuObj.AddComponent<MainMenu>();

        if (gameScenePath != null)
        {
            var so = new SerializedObject(menu);
            so.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(gameScenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MenuScenePath));
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        FixBuildSceneList();
        Debug.Log($"Lighthouse: created {MenuScenePath}. Press Play here to try the menu.");
    }

    [MenuItem("Lighthouse/Fix Build Scene List")]
    public static void FixBuildSceneList()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != MenuScenePath).ToList();
        string gamePath = FindGameScenePath();
        if (gamePath != null && scenes.All(s => s.path != gamePath))
            scenes.Add(new EditorBuildSettingsScene(gamePath, true));
        if (File.Exists(MenuScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Lighthouse: build scene list is now: " + string.Join(", ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    [MenuItem("Lighthouse/Show Tutorial Letter Again")]
    public static void ForgetTutorialSeen()
    {
        PlayerPrefs.DeleteKey("TutorialLetterSeen");
        Debug.Log("Lighthouse: the keeper's letter will show again on the next play.");
    }

    // ------------------------------------------------------------------ Helpers

    private static void RemoveMissingScripts(List<string> log)
    {
        int removed = 0;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0) continue;
                Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove missing scripts");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
        }
        if (removed > 0) log.Add($"Removed {removed} missing-script component(s).");
    }

    private static T Ensure<T>(GameObject host, List<string> log) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null) return existing;
        log.Add($"Added {typeof(T).Name}.");
        return Undo.AddComponent<T>(host);
    }

    private static void AssignAudio(GameObject host, TimeManager time, NoteInputHandler notes, List<string> log)
    {
        // Music
        var timeSo = new SerializedObject(time);
        var musicSource = timeSo.FindProperty("audioSource");
        if (musicSource.objectReferenceValue == null)
        {
            musicSource.objectReferenceValue = Undo.AddComponent<AudioSource>(host);
            log.Add("Added an AudioSource for the music.");
        }
        var musicClip = timeSo.FindProperty("audioClip");
        if (musicClip.objectReferenceValue == null)
        {
            var clip = FindAsset<AudioClip>(DefaultMusicName) ?? FirstAssetIn<AudioClip>("Assets/Music");
            if (clip != null)
            {
                musicClip.objectReferenceValue = clip;
                log.Add($"Set the music to {clip.name}.");
            }
            else log.Add("No music clip found: assign one on TimeManager.");
        }
        timeSo.ApplyModifiedProperties();

        // Notes
        var notesSo = new SerializedObject(notes);
        var sfx = notesSo.FindProperty("sfxAudioSource");
        if (sfx.objectReferenceValue == null)
        {
            sfx.objectReferenceValue = musicSource.objectReferenceValue;
            log.Add("Pointed NoteInputHandler at the music AudioSource for note sounds.");
        }

        int filled = 0;
        foreach (string chord in new[] { "C", "F", "G" })
        {
            var array = notesSo.FindProperty($"clips{chord}");
            if (array.arraySize < 5) array.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                var slot = array.GetArrayElementAtIndex(i);
                if (slot.objectReferenceValue != null) continue;
                var clip = FindAsset<AudioClip>($"Piano{chord}_{i + 1}");
                if (clip == null) continue;
                slot.objectReferenceValue = clip;
                filled++;
            }
        }
        notesSo.ApplyModifiedProperties();
        if (filled > 0) log.Add($"Filled {filled} note clip slot(s) from the Piano clips.");
    }

    private static void AssignShipPrefabs(ShipManager ships, List<string> log)
    {
        var so = new SerializedObject(ships);
        var table = so.FindProperty("spawnTable");
        int filled = 0;
        for (int i = 0; i < table.arraySize; i++)
        {
            var entry = table.GetArrayElementAtIndex(i);
            var prefab = entry.FindPropertyRelative("prefab");
            if (prefab.objectReferenceValue != null) continue;

            var typeProp = entry.FindPropertyRelative("type");
            string typeName = typeProp.enumNames[typeProp.enumValueIndex];
            var asset = FindAsset<GameObject>(typeName, "t:Prefab");
            if (asset == null) continue;
            prefab.objectReferenceValue = asset;
            filled++;
        }
        so.ApplyModifiedProperties();
        if (filled > 0) log.Add($"Assigned {filled} ship prefab(s) by name.");
    }

    private static string FindGameScenePath()
    {
        var fromBuild = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => p != MenuScenePath && File.Exists(p));
        if (fromBuild != null) return fromBuild;
        const string sample = "Assets/Scenes/SampleScene.unity";
        return File.Exists(sample) ? sample : null;
    }

    private static T FindAsset<T>(string name, string filter = null) where T : Object
    {
        filter ??= $"t:{typeof(T).Name}";
        foreach (var guid in AssetDatabase.FindAssets($"{name} {filter}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }

    private static T FirstAssetIn<T>(string folder) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        var guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }).FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
