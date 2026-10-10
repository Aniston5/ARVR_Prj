using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PlanterAndroidBuild
{
    [MenuItem("AR Planter/Build Android APK")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode before building the Android app.");
            return;
        }
        Directory.CreateDirectory("Builds/Android");
        string statusPath = "Builds/Android/build-status.txt";
        File.WriteAllText(statusPath, "Building Android APK...\n" + DateTime.Now);
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("Could not switch to Android.");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MainAR.unity" },
                locationPathName = "Builds/Android/ARPlanter.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            var status = new StringBuilder();
            status.AppendLine(report.summary.result.ToString());
            status.AppendLine("Errors: " + report.summary.totalErrors);
            status.AppendLine("APK: " + report.summary.outputPath);
            status.AppendLine("Duration: " + report.summary.totalTime);
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        status.AppendLine(message.content);
            File.WriteAllText(statusPath, status.ToString());
            Debug.Log(status.ToString());
        }
        catch (Exception exception)
        {
            File.WriteAllText(statusPath, "Failed\n" + exception);
            Debug.LogException(exception);
        }
    }
}
