using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(HierarchyIconRoot))]
public class HierarchyIconRootEditor : Editor
{
    void OnEnable()
    {
        Refresh();
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Hierarchy icons are auto-generated from components.", MessageType.Info);
    }

    void Refresh()
    {
        var root = (HierarchyIconRoot)target;
        root.markers = root.GetComponentsInChildren<HierarchyIconMarker>(true).ToList();
        EditorUtility.SetDirty(root);
    }
}
