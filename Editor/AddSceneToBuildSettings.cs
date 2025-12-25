using UnityEditor;
using UnityEngine;
using System.Linq;

public static class AddSceneToBuildSettings
{
    private const string MENU_PATH = "Assets/Add to Build Settings";

    // Validation: show menu only for Scene assets
    [MenuItem(MENU_PATH, true)]
    private static bool ValidateAddScene()
    {
        return Selection.activeObject is SceneAsset;
    }

    [MenuItem(MENU_PATH)]
    private static void AddSelectedScene()
    {
        var scene = Selection.activeObject as SceneAsset;
        if (scene == null)
            return;

        string scenePath = AssetDatabase.GetAssetPath(scene);

        // Get existing build scenes
        var scenes = EditorBuildSettings.scenes.ToList();

        // Check if already exists
        if (scenes.Any(s => s.path == scenePath))
        {
            EditorUtility.DisplayDialog(
                "Scene Already Added",
                $"The scene '{scene.name}' is already in the Build Settings.",
                "OK"
            );
            return;
        }

        // Add scene
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"Added scene to Build Settings: {scenePath}");
    }
}
