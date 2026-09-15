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
            // Every build gets its own file names (content hashes). GitHub Pages lets browsers cache
            // files for 10 minutes; with fixed names a reload right after a deploy could mix old code
            // with new data and crash while loading. Hashed names can be cached forever instead,
            // so Unity's own IndexedDB cache isn't needed.
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = false;
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
            if (s.result == BuildResult.Succeeded) WriteManifest(outDir);
            Debug.Log($"[WebGLBuilder] {s.result}: {s.totalSize / 1048576f:0.0} MB, {s.totalTime.TotalSeconds:0} s, {s.totalErrors} errors -> {outDir}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// build.json names this build's (hashed) files. index.html fetches it fresh on every visit, so a
        /// page served from the browser cache still loads the newest build.
        /// </summary>
        static void WriteManifest(string outDir)
        {
            string dir = System.IO.Path.Combine(outDir, "Build");
            string Find(string pattern)
            {
                var hits = System.IO.Directory.GetFiles(dir, pattern);
                return hits.Length > 0 ? "Build/" + System.IO.Path.GetFileName(hits[0]) : "";
            }
            string json = "{\n" +
                "  \"loader\": \"" + Find("*.loader.js") + "\",\n" +
                "  \"data\": \"" + Find("*.data*") + "\",\n" +
                "  \"framework\": \"" + Find("*.framework.js*") + "\",\n" +
                "  \"code\": \"" + Find("*.wasm*") + "\"\n}\n";
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "build.json"), json);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
