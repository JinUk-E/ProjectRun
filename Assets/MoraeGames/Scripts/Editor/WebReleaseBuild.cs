using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MoraeGames.Editor
{
    /// <summary>Creates a self-contained static bundle for a future Starlight Workshop release.</summary>
    public static class WebReleaseBuild
    {
        [MenuItem("MoraeGames/Build Starlight Workshop Web")]
        public static void Run()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for Unity 6000.3.6f1.");
            ReleaseReadinessRepair.Verify();
            PlayerSettings.WebGL.template = "PROJECT:StarlightWorkshop";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.WebGL,
                locationPathName = "Builds/StarlightWorkshopWeb",
                options = BuildOptions.None
            });
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/web-build.txt", report.summary.result + "; errors=" + report.summary.totalErrors + "; bytes=" + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Web build failed");
            File.Copy("Docs/StarlightWebDeployment.txt", "Builds/StarlightWorkshopWeb/DEPLOYMENT.txt", true);
            string credits = "Builds/StarlightWorkshopWeb/CREDITS.txt";
            using (var writer = new StreamWriter(credits))
            {
                writer.WriteLine("별빛공방 · 경찰과 도둑 / Third-party art notices\n");
                foreach (var file in Directory.GetFiles("Assets/MoraeGames/Art", "*.txt", SearchOption.AllDirectories)
                    .Concat(new[] { "Assets/MoraeGames/Fonts/OFL.txt", "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt" }))
                {
                    writer.WriteLine("\n--- " + file + " ---\n");
                    writer.WriteLine(File.ReadAllText(file));
                }
            }
        }
    }
}
