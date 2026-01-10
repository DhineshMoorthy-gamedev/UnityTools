using UnityEditor;
using UnityEngine;
using System.IO;

public static class QuickPrefabCreator
{
    [MenuItem("GameObject/Prefab/Make Prefab", false, 0)]
    private static void MakePrefab()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[Make Prefab] No GameObject selected.");
            return;
        }

        string prefabFolderPath = GetOrCreatePrefabFolder();
        string prefabPath = Path.Combine(prefabFolderPath, selected.name + ".prefab");
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            selected,
            prefabPath,
            InteractionMode.UserAction
        );

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"[Make Prefab] Prefab created at: {prefabPath}", prefab);
    }

    [MenuItem("GameObject/Prefab/Make Prefab", true)]
    private static bool ValidateMakePrefab()
    {
        return Selection.activeGameObject != null;
    }

    private static string GetOrCreatePrefabFolder()
    {
        if (AssetDatabase.IsValidFolder("Assets/Prefabs"))
            return "Assets/Prefabs";

        if (AssetDatabase.IsValidFolder("Assets/Prefab"))
            return "Assets/Prefab";

        AssetDatabase.CreateFolder("Assets", "Prefabs");
        return "Assets/Prefabs";
    }
}
