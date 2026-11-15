using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play always starts Main.unity, whichever scene is open in the Editor
/// (the app is one scene; this avoids accidentally playing an empty one).
/// </summary>
[InitializeOnLoad]
static class PlayFromMainScene
{
    const string MainScene = "Assets/Scenes/Main.unity";

    static PlayFromMainScene()
    {
        EditorApplication.delayCall += () =>
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene);
    }
}
