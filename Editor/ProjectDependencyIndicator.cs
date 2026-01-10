#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dhinesh.EditorTools.ProjectTools
{
    [InitializeOnLoad]
    public static class ProjectDependencyIndicator
    {
        static readonly Dictionary<string, int> dependencyCount = new();

        static ProjectDependencyIndicator()
        {
            BuildDependencyMap();
            EditorApplication.projectWindowItemOnGUI += OnProjectGUI;
            EditorApplication.projectChanged += BuildDependencyMap;
        }

        static void BuildDependencyMap()
        {
            dependencyCount.Clear();

            foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (!assetPath.StartsWith("Assets/"))
                    continue;

                var deps = AssetDatabase.GetDependencies(assetPath, true);
                foreach (var dep in deps)
                {
                    if (dep == assetPath)
                        continue;

                    dependencyCount.TryGetValue(dep, out int c);
                    dependencyCount[dep] = c + 1;
                }
            }

            EditorApplication.RepaintProjectWindow();
        }

        static void OnProjectGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            if (!dependencyCount.TryGetValue(path, out int count) || count <= 0)
                return;

            bool isIconView = rect.height > 20f;

            if (isIconView)
                DrawIconView(rect, count);
            else
                DrawListView(rect, count);
        }

        static void DrawListView(Rect rect, int count)
        {
            const float width = 28f;
            const float padding = 4f;

            var countRect = new Rect(
                rect.xMax - width - padding,
                rect.y,
                width,
                rect.height
            );

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal =
                {
                    textColor = Color.yellow
                },
                fontSize = 12
            };

            GUI.Label(countRect, count.ToString(),style);
        }

        static void DrawIconView(Rect rect, int count)
        {
            const float width = 26f;
            const float height = 16f;
            const float rightPadding = 2f;
            const float bottomPadding = 6f; // spacing from filename

            var countRect = new Rect(
                rect.xMax - width - rightPadding,
                rect.yMax - height - bottomPadding,
                width,
                height
            );

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal =
                {
                    textColor = Color.yellow
                },
                fontSize = 12
            };

            GUI.Label(countRect, count.ToString(), style);
        }

    }
}
#endif
