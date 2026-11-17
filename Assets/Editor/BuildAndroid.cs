using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the Android APK to Builds/Android/PostureCheck.apk.
/// Menu: Tools > Posture Check > Build Android APK. Batch: -executeMethod BuildAndroid.Build
/// </summary>
public static class BuildAndroid
{
    const string OutputPath = "Builds/Android/PostureCheck.apk";

    [MenuItem("Tools/Posture Check/Build Android APK")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        ArSetup.Apply();
        SceneWiring.Wire(); // make sure Main.unity has every component before building

        EditorUserBuildSettings.buildAppBundle = false;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development, // keeps Debug.Log output readable with adb logcat
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"BUILD-RESULT {summary.result} {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime:mm\\:ss} -> {OutputPath}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
