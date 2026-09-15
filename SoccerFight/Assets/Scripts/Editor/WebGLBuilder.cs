using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoccerFight.EditorTools
{
    /// <summary>
    /// Batch-mode WebGL build for GitHub Pages. Pages cannot send Content-Encoding headers, so the
    /// build is gzip-compressed with the JS decompression fallback (works on any static host).
    /// Usage: Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod SoccerFight.EditorTools.WebGLBuilder.Build -sfOut &lt;dir&gt;
    /// </summary>
    public static class WebGLBuilder
    {
        public static void Build()
        {
            string outDir = Arg("-sfOut") ?? "Builds/WebGL";

            PlayerSettings.WebGL.template = "PROJECT:SoccerFight";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SplashScreen.show = false;   // optional for Personal since Unity 6

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[WebGLBuilder] {s.result}: {s.totalSize / 1048576f:0.0} MB, {s.totalTime.TotalSeconds:0} s, {s.totalErrors} errors -> {outDir}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
