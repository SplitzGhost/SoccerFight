using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerFight.EditorTools
{
    /// <summary>Creates the Game scene (one GameObject with the Game component) and registers it for builds.</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("SoccerFight/Create Game Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Game");
            go.AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SoccerFight] Game scene created at " + ScenePath);
        }

        /// <summary>Batch-mode entry point (-executeMethod SoccerFight.EditorTools.ProjectSetup.Batch).</summary>
        public static void Batch()
        {
            CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
