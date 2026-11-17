using PostureCheck;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the "App" object in Main.unity with the camera, pose tracker, overlay, measurement, debug view, floor scanner and flow and assigns
/// their asset references. Safe to run more than once.
/// Runs by itself after scripts compile when Main.unity is missing one of the components.
/// Menu: Tools > Posture Check > Wire Main Scene. Batch: -executeMethod SceneWiring.Wire
/// </summary>
[InitializeOnLoad]
public static class SceneWiring
{
    const string MainScene = "Assets/Scenes/Main.unity";

    static readonly System.Type[] Components =
        { typeof(FrontCamera), typeof(PoseTracker), typeof(PoseSmoother), typeof(Overlay), typeof(Measure), typeof(PoseDebugView),
          typeof(FloorScanner), typeof(FlowController) };

    static SceneWiring()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || !NeedsWiring()) return;
            Debug.Log("Main.unity is missing components; wiring it.");
            Wire();
        };
    }

    /// <summary>True when the saved scene file does not reference every component script.</summary>
    static bool NeedsWiring()
    {
        string path = System.IO.Path.GetFullPath(MainScene);
        if (!System.IO.File.Exists(path)) return false;
        string text = System.IO.File.ReadAllText(path);
        foreach (var type in Components)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:MonoScript {type.Name}"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type && !text.Contains(guid)) return true;
            }
        }
        return false;
    }

    [MenuItem("Tools/Posture Check/Wire Main Scene")]
    public static void Wire()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != MainScene)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MainScene);
        }

        var app = GameObject.Find("App");
        if (app == null) app = new GameObject("App");
        var cam = GetOrAdd<FrontCamera>(app);
        var pose = GetOrAdd<PoseTracker>(app);
        var smoother = GetOrAdd<PoseSmoother>(app);
        var overlay = GetOrAdd<Overlay>(app);
        var measure = GetOrAdd<Measure>(app);
        var debug = GetOrAdd<PoseDebugView>(app);
        var scanner = GetOrAdd<FloorScanner>(app);
        var flow = GetOrAdd<FlowController>(app);

        Set(cam, "uprightBlitShader", Load<Shader>("Assets/Shaders/UprightBlit.shader"));

        Set(pose, "frontCamera", cam);
        Set(pose, "detectorModel", Load<ModelAsset>("Assets/Models/pose_detection.onnx"));
        Set(pose, "landmarkerModel", Load<ModelAsset>("Assets/Models/pose_landmarks_detector_lite.onnx"));
        Set(pose, "anchorsCsv", Load<TextAsset>("Assets/Models/anchors.csv"));
        Set(pose, "imageToTensor", Load<ComputeShader>("Assets/Shaders/ImageToTensor.compute"));

        Set(smoother, "poseTracker", pose);
        Set(smoother, "frontCamera", cam);

        Set(overlay, "frontCamera", cam);
        Set(overlay, "pose", smoother);

        Set(measure, "frontCamera", cam);
        Set(measure, "pose", smoother);
        Set(measure, "overlay", overlay);

        Set(debug, "frontCamera", cam);
        Set(debug, "poseTracker", pose);
        Set(debug, "measure", measure);

        Set(scanner, "floorMarker", Load<Texture2D>("Assets/Content/floor_marker.png"));

        Set(flow, "floorScanner", scanner);
        Set(flow, "frontCamera", cam);
        var sceneCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        Set(flow, "sceneCamera", sceneCamera);

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
