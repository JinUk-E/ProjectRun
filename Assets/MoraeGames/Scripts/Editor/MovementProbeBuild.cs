using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MoraeGames.Editor
{
    public static class MovementProbeBuild
    {
        public static void Run()
        {
            MovementFeelChecks.Run();
            ReleaseReadinessRepair.Verify();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/MovementProbe/CopsAndRobbers.exe",
                options = BuildOptions.Development
            });
            File.WriteAllText("TestResults/movement-probe-build.txt", report.summary.result + "; errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Movement probe build failed");
        }
    }
}
