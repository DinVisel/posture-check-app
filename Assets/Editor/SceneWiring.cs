using PostureCheck;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sets up the "App" object in Main.unity with the camera, pose tracker and debug view and assigns
/// their asset references. Safe to run more than once.
/// Menu: Tools > Posture Check > Wire Main Scene. Batch: -executeMethod SceneWiring.Wire
/// </summary>
public static class SceneWiring
{
    [MenuItem("Tools/Posture Check/Wire Main Scene")]
    public static void Wire()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

        var app = GameObject.Find("App");
        if (app == null) app = new GameObject("App");
        var cam = GetOrAdd<FrontCamera>(app);
        var pose = GetOrAdd<PoseTracker>(app);
        var debug = GetOrAdd<PoseDebugView>(app);

        Set(cam, "uprightBlitShader", Load<Shader>("Assets/Shaders/UprightBlit.shader"));

        Set(pose, "frontCamera", cam);
        Set(pose, "detectorModel", Load<ModelAsset>("Assets/Models/pose_detection.onnx"));
        Set(pose, "landmarkerModel", Load<ModelAsset>("Assets/Models/pose_landmarks_detector_lite.onnx"));
        Set(pose, "anchorsCsv", Load<TextAsset>("Assets/Models/anchors.csv"));
        Set(pose, "imageToTensor", Load<ComputeShader>("Assets/Shaders/ImageToTensor.compute"));

        Set(debug, "frontCamera", cam);
        Set(debug, "poseTracker", pose);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("WIRING-DONE");
    }

    // Explicit null check: Unity's overloaded == treats destroyed/missing objects as null, ?? does not.
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception($"Missing asset: {path} ({typeof(T).Name})");
        return asset;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field) ?? throw new System.Exception($"{target.GetType().Name} has no field '{field}'");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
