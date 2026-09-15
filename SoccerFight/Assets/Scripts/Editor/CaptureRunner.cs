using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerFight.EditorTools
{
    /// <summary>
    /// Batch-mode helper: opens the Game scene, waits until imports/compilation have settled,
    /// enters play mode (the CaptureDriver then runs its scripted scenario) and quits the editor
    /// once it reports completion. Survives domain reloads via [InitializeOnLoad] + SessionState.
    /// Usage: Unity -batchmode -projectPath . -executeMethod SoccerFight.EditorTools.CaptureRunner.Run -sfCapture all -sfOut &lt;dir&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class CaptureRunner
    {
        const string KeyActive = "sf_capture_active";
        const string KeyStart = "sf_capture_start";
        static double stableSince = -1;

        static CaptureRunner()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sfCapture") < 0) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SessionState.SetBool(KeyActive, true);
            SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            double now = EditorApplication.timeSinceStartup;

            if (now - SessionState.GetFloat(KeyStart, (float)now) > 540.0)
            {
                Debug.LogError("[CaptureRunner] timeout");
                Quit(2);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    stableSince = -1;
                    return;
                }
                if (stableSince < 0) stableSince = now;
                if (now - stableSince > 4.0)
                {
                    Debug.Log("[CaptureRunner] editor settled, entering play mode");
                    EditorApplication.EnterPlaymode();
                }
                return;
            }

            if (CaptureDriver.Finished)
            {
                Debug.Log("[CaptureRunner] done, exiting");
                Quit(0);
            }
        }

        static void Quit(int code)
        {
            SessionState.SetBool(KeyActive, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
