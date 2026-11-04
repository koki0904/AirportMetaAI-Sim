using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AirportSim
{
    /// <summary>バッチ実験用の Windows プレイヤーをビルドする。tools/run_batch.ps1 から実行する。</summary>
    public static class BatchBuild
    {
        const string ScenePath = "Assets/_Project/Scenes/AirportSim.unity";
        const string OutPath = "Builds/AirportSim/AirportSim.exe";

        [MenuItem("AirportSim/Build Batch Player")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("シーンがありません。先に AirportSim > Build Airport Scene を実行してください: " + ScenePath);
                return;
            }
            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opt);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("Batch player built: " + Path.GetFullPath(OutPath));
            else
                Debug.LogError("Build failed: " + report.summary.result);
        }
    }
}
