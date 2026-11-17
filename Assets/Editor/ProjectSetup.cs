using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

/// <summary>
/// Applies the project's player, XR and folder settings. Safe to run more than once.
/// Menu: Tools > Posture Check > Apply Project Settings. Batch: -executeMethod ProjectSetup.Apply
/// </summary>
public static class ProjectSetup
{
    const string BundleId = "com.nmed.posturecheck";
    const string MainScene = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/Posture Check/Apply Project Settings")]
    public static void Apply()
    {
        Player();
        Android();
        IOS();
        XR();
        Folders();
        Scenes();
        AssetDatabase.SaveAssets();
        Debug.Log("SETUP-DONE");
    }

    static void Player()
    {
        PlayerSettings.companyName = "NMED";
        PlayerSettings.productName = "Posture Check";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    static void Android()
    {
        var t = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(t, BundleId);
        PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        // Vulkan first for GPU inference, OpenGLES3 as fallback (ARCore project validation rule).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
    }

    static void IOS()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.cameraUsageDescription =
            "The camera finds the floor and tracks posture. Nothing is recorded or uploaded.";
    }

    static void XR()
    {
        // The floor scan can be skipped (ankle-based fallback), so AR is optional on both platforms.
        var arcore = Persist(ARCoreSettings.GetOrCreateSettings(), "Assets/XR/Settings/ARCoreSettings.asset");
        arcore.requirement = ARCoreSettings.Requirement.Optional;
        ARCoreSettings.currentSettings = arcore;
        EditorUtility.SetDirty(arcore);

        var arkit = Persist(ARKitSettings.GetOrCreateSettings(), "Assets/XR/Settings/ARKitSettings.asset");
        arkit.requirement = ARKitSettings.Requirement.Optional;
        ARKitSettings.currentSettings = arkit;
        EditorUtility.SetDirty(arkit);

        EnableLoader(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
        EnableLoader(BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");
        ArSetup.Apply();
    }

    // GetOrCreateSettings returns an in-memory object the first time; it must be an asset before
    // it can be registered as a build config object.
    static T Persist<T>(T settings, string path) where T : ScriptableObject
    {
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings))) return settings;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(settings, path);
        return settings;
    }

    static void EnableLoader(BuildTargetGroup group, string loader)
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
        }
        if (!perTarget.HasManagerSettingsForBuildTarget(group))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

        var manager = perTarget.ManagerSettingsForBuildTarget(group);
        if (!XRPackageMetadataStore.IsLoaderAssigned(loader, group))
            XRPackageMetadataStore.AssignLoader(manager, loader, group);
        EditorUtility.SetDirty(perTarget);
    }

    static void Folders()
    {
        foreach (var d in new[] { "Assets/Scripts", "Assets/Content", "Assets/StreamingAssets" })
            Directory.CreateDirectory(d);

        // Placeholders the design team replaces (same file names = no code change).
        WritePlaceholder("Assets/Content/mascot_idle.png", 512, new Color(0.6f, 0.6f, 0.6f), Circle);
        WritePlaceholder("Assets/Content/floor_marker.png", 512, new Color(1f, 0.85f, 0.2f), Star);
        AssetDatabase.Refresh();
    }

    static void Scenes()
    {
        const string sample = "Assets/Scenes/SampleScene.unity";
        if (File.Exists(sample) && !File.Exists(MainScene))
            AssetDatabase.MoveAsset(sample, MainScene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };

        foreach (var junk in new[] { "Assets/TutorialInfo", "Assets/Readme.asset" })
            if (AssetDatabase.IsValidFolder(junk) || File.Exists(junk))
                AssetDatabase.DeleteAsset(junk);
    }

    // --- placeholder image helpers ---

    delegate bool Shape(float x, float y); // x, y in [-1, 1]; true = inside

    static readonly Shape Circle = (x, y) => x * x + y * y <= 0.81f;

    // Five-pointed star, one tip straight up: 10-vertex polygon alternating outer/inner radius.
    static readonly Shape Star = (x, y) =>
    {
        var v = new Vector2[10];
        for (int k = 0; k < 10; k++)
        {
            float ang = Mathf.PI / 2f + k * Mathf.PI / 5f;
            float r = k % 2 == 0 ? 0.95f : 0.38f;
            v[k] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
        }
        bool inside = false; // even-odd ray casting
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if ((v[i].y > y) != (v[j].y > y) &&
                x < (v[j].x - v[i].x) * (y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        return inside;
    };

    static void WritePlaceholder(string path, int size, Color color, Shape inside)
    {
        if (File.Exists(path)) return; // never overwrite a delivered asset
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int j = 0; j < size; j++)
        for (int i = 0; i < size; i++)
        {
            float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
            var c = color;
            c.a = inside(x, y) ? 1f : 0f;
            px[j * size + i] = c;
        }
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
