using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchyIconDrawer
{
    static HierarchyIconDrawer()
    {
        EditorApplication.hierarchyWindowItemOnGUI += Draw;
    }

    static void Draw(int instanceID, Rect rect)
    {
        GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (!go) return;

        var root = go.GetComponent<HierarchyIconRoot>();
        if (!root || root.markers == null || root.markers.Count == 0)
            return;

        DrawIconsRight(rect, root);
    }

    static void DrawIconsRight(Rect rect, HierarchyIconRoot root)
    {
        const float iconSize = 16f;
        const float padding = 2f;
        const float rightPadding = 60f; // avoid hierarchy buttons

        float x = rect.xMax - rightPadding - (root.markers.Count * (iconSize + padding));

        foreach (var marker in root.markers)
        {
            if (!marker) continue;

            var icon = ResolveIcon(marker.gameObject);
            if (!icon) continue;

            Rect iconRect = new Rect(
                x,
                rect.y,
                iconSize,
                iconSize
            );

            GUIContent content = new GUIContent(icon, marker.gameObject.name);

            if (GUI.Button(iconRect, content, GUIStyle.none))
            {
                Selection.activeGameObject = marker.gameObject;
                EditorGUIUtility.PingObject(marker.gameObject);
            }

            x += iconSize + padding;
        }
    }

    static Texture ResolveIcon(GameObject go)
    {
        // Prefer non-Transform components
        var comps = go.GetComponents<Component>();

        foreach (var c in comps)
        {
            if (c is Transform) continue;

            var content = EditorGUIUtility.ObjectContent(null, c.GetType());
            if (content.image != null)
                return content.image;
        }

        // Fallback to GameObject icon
        return EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
    }
}
