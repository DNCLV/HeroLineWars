using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Keeps player builds and ordinary Editor Play sessions entering through hero selection.
/// The preference is per developer and never modifies or saves the currently open scene.
/// </summary>
[InitializeOnLoad]
public static class MainMenuStartupEditor
{
    const string MainMenuPath = "Assets/Scenes/HLW_MainMenu.unity";
    const string GameplayPath = "Assets/Scenes/HLW_LanePrototype.unity";
    const string PreferenceKey = "HLW.StartPlayModeFromMainMenu";
    const string DirectPlaySessionKey = "HLW.DirectScenePlayOnce";
    const string ToggleMenuPath = "Tools/Hero Line Wars/Editor/Start Play Mode From Main Menu";
    const string DirectPlayMenuPath = "Tools/Hero Line Wars/Editor/Play Current Scene Once";

    static MainMenuStartupEditor()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += ConfigureStartup;
    }

    public static bool StartFromMainMenu
    {
        get => EditorPrefs.GetBool(PreferenceKey, true);
        private set => EditorPrefs.SetBool(PreferenceKey, value);
    }

    [MenuItem(ToggleMenuPath)]
    static void ToggleStartScene()
    {
        StartFromMainMenu = !StartFromMainMenu;
        SessionState.EraseBool(DirectPlaySessionKey);
        ConfigurePlayModeStartScene();
        Debug.Log(StartFromMainMenu
            ? "Hero Line Wars Play Mode will start from HLW_MainMenu."
            : "Hero Line Wars automatic Main Menu startup is disabled for this developer.");
    }

    [MenuItem(ToggleMenuPath, true)]
    static bool ValidateToggleStartScene()
    {
        Menu.SetChecked(ToggleMenuPath, StartFromMainMenu);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(DirectPlayMenuPath)]
    static void PlayCurrentSceneOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(DirectPlaySessionKey, true);
        EditorSceneManager.playModeStartScene = null;
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(DirectPlayMenuPath, true)]
    static bool ValidatePlayCurrentSceneOnce() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void ConfigureStartup()
    {
        EnsureBuildSceneOrder();
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            ConfigurePlayModeStartScene();
    }

    static void ConfigurePlayModeStartScene()
    {
        bool directPlayOnce = SessionState.GetBool(DirectPlaySessionKey, false);
        SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
        if (StartFromMainMenu && !directPlayOnce && menuScene)
            EditorSceneManager.playModeStartScene = menuScene;
        else if (!StartFromMainMenu || directPlayOnce || EditorSceneManager.playModeStartScene == menuScene)
            EditorSceneManager.playModeStartScene = null;
        Menu.SetChecked(ToggleMenuPath, StartFromMainMenu);
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetBool(DirectPlaySessionKey, false))
            SessionState.EraseBool(DirectPlaySessionKey);
        ConfigurePlayModeStartScene();
    }

    static void EnsureBuildSceneOrder()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>(current.Length + 2)
        {
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(GameplayPath, true)
        };
        ordered.AddRange(current.Where(scene => scene.path != MainMenuPath && scene.path != GameplayPath));

        bool changed = current.Length != ordered.Count;
        for (int i = 0; !changed && i < current.Length; i++)
            changed = current[i].path != ordered[i].path || current[i].enabled != ordered[i].enabled;
        if (changed) EditorBuildSettings.scenes = ordered.ToArray();
    }
}
