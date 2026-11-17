using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

/// <summary>
/// AR settings the floor scan needs (step 1.5). Runs by itself after scripts compile when
/// something is missing, and from Tools > Posture Check > Apply Project Settings.
/// - URP renderers get the "AR Background" feature, without which the back camera image is black,
///   and the "AR Command Buffer Support" feature that ARCore needs with Vulkan (otherwise also black).
/// - XR does not start with the app: the floor scan starts it and stops it before the front camera opens.
/// - XR Simulation is the PC (Editor) loader, so the scan can be tried in Play mode without a phone.
/// </summary>
[InitializeOnLoad]
public static class ArSetup
{
    static readonly string[] Renderers = { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" };
    static readonly BuildTargetGroup[] Groups = { BuildTargetGroup.Android, BuildTargetGroup.iOS, BuildTargetGroup.Standalone };
    const string SimulationLoader = "UnityEngine.XR.Simulation.SimulationLoader";

    static ArSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !NeedsApply()) return;
            Debug.Log("Applying AR settings for the floor scan.");
            Apply();
        };
    }

    static bool NeedsApply()
    {
        foreach (var path in Renderers)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data != null && (!Has<ARBackgroundRendererFeature>(data) || !Has<ARCommandBufferSupportRendererFeature>(data))) return true;
        }
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget)
            || perTarget == null) return false; // ProjectSetup has not run yet
        foreach (var group in Groups)
        {
            var settings = perTarget.SettingsForBuildTarget(group);
            if (settings == null || settings.InitManagerOnStart) return true;
        }
        return !XRPackageMetadataStore.IsLoaderAssigned(SimulationLoader, BuildTargetGroup.Standalone);
    }

    public static void Apply()
    {
        foreach (var path in Renderers)
        {
            AddFeature<ARBackgroundRendererFeature>(path);
            AddFeature<ARCommandBufferSupportRendererFeature>(path);
        }

        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget)
            && perTarget != null)
        {
            foreach (var group in Groups)
            {
                if (!perTarget.HasManagerSettingsForBuildTarget(group))
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                var settings = perTarget.SettingsForBuildTarget(group);
                settings.InitManagerOnStart = false;
                EditorUtility.SetDirty(settings);
            }
            if (!XRPackageMetadataStore.IsLoaderAssigned(SimulationLoader, BuildTargetGroup.Standalone))
                XRPackageMetadataStore.AssignLoader(perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone),
                    SimulationLoader, BuildTargetGroup.Standalone);
            EditorUtility.SetDirty(perTarget);
        }
        AssetDatabase.SaveAssets();
    }

    static bool Has<T>(ScriptableRendererData data) where T : ScriptableRendererFeature =>
        data.rendererFeatures.Any(f => f is T);

    static void AddFeature<T>(string path) where T : ScriptableRendererFeature
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
        if (data == null || Has<T>(data)) return;

        // Same steps as the renderer's "Add Renderer Feature" button: sub-asset + feature list + id map.
        var feature = ScriptableObject.CreateInstance<T>();
        feature.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        var so = new SerializedObject(data);
        var features = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }
}
