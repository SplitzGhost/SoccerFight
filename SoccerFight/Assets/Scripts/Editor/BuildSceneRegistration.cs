using System.Linq;
using UnityEditor;

namespace SoccerFight.EditorTools
{
    /// <summary>Makes sure the Game scene is the first scene in the build so a player build starts the game.</summary>
    [InitializeOnLoad]
    static class BuildSceneRegistration
    {
        static BuildSceneRegistration()
        {
            EditorApplication.delayCall += Ensure;
        }

        static void Ensure()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath) == null) return;
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Count > 0 && scenes[0].path == ProjectSetup.ScenePath && scenes[0].enabled) return;
            scenes.RemoveAll(s => s.path == ProjectSetup.ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ProjectSetup.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
