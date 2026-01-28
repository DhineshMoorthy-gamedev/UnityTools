using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
public class SkinnedToMeshRendererConverter : Editor
{
    [MenuItem("Tools/GameDevTools/Convert Skinned Mesh to Static Mesh", false, 100)]
    public static void ConvertSelected()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("Please select GameObjects with SkinnedMeshRenderers.");
            return;
        }
        int count = 0;
        List<GameObject> createdObjects = new List<GameObject>();
        foreach (GameObject obj in selectedObjects)
        {
            // 1. Create a clone
            GameObject clone = Instantiate(obj, obj.transform.parent);
            clone.name = obj.name + "_Static";
            Undo.RegisterCreatedObjectUndo(clone, "Convert Skinned Mesh to Static Mesh");
            createdObjects.Add(clone);
            // 2. Remove Animators to prevent the pose from being overwritten
            Animator[] animators = clone.GetComponentsInChildren<Animator>();
            foreach (var anim in animators)
            {
                DestroyImmediate(anim);
            }
            // 3. We search for SkinnedMeshRenderer on the clone and its children
            SkinnedMeshRenderer[] renderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var smr in renderers)
            {
                ConvertSingle(smr);
                count++;
            }
        }
        // Optional: Select the newly created objects
        Selection.objects = createdObjects.ToArray();
        Debug.Log($"Successfully created {createdObjects.Count} static copies with {count} converted MeshRenderers.");
    }
    private static void ConvertSingle(SkinnedMeshRenderer smr)
    {
        GameObject targetObj = smr.gameObject;
        // 1. Bake the mesh to capture current pose
        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh);
        bakedMesh.name = smr.sharedMesh.name + "_Baked";
        // 2. Set up MeshFilter and MeshRenderer
        MeshFilter meshFilter = targetObj.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = targetObj.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = bakedMesh;
        MeshRenderer meshRenderer = targetObj.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = targetObj.AddComponent<MeshRenderer>();
        }
        meshRenderer.sharedMaterials = smr.sharedMaterials;
        // 3. Remove the SkinnedMeshRenderer
        DestroyImmediate(smr);
    }
    [MenuItem("Tools/GameDevTools/Convert Skinned Mesh to Static Mesh", true, 100)]
    public static bool ValidateConvertSelected()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0) return false;
        foreach (var obj in Selection.gameObjects)
        {
            if (obj.GetComponentInChildren<SkinnedMeshRenderer>() != null) return true;
        }
        return false;
    }
}