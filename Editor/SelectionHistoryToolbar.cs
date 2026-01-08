#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dhinesh.EditorTools.CustomTaskbar
{
    [InitializeOnLoad]
    public static class SelectionHistoryManager
    {
        public static int MaxHistory = 10;

        static readonly List<GameObject> history = new();
        static int currentIndex = -1;
        static bool suppressTracking;

        static SelectionHistoryManager()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        static void OnSelectionChanged()
        {
            if (suppressTracking) return;

            var go = Selection.activeGameObject;
            if (go == null) return;

            // Ignore duplicate selection
            if (currentIndex >= 0 && history[currentIndex] == go)
                return;

            // Trim forward history
            if (currentIndex < history.Count - 1)
                history.RemoveRange(currentIndex + 1, history.Count - currentIndex - 1);

            history.Add(go);

            // Clamp history size
            if (history.Count > MaxHistory)
            {
                history.RemoveAt(0);
            }

            currentIndex = history.Count - 1;
        }

        public static bool CanGoBack => currentIndex > 0;
        public static bool CanGoForward => currentIndex < history.Count - 1;

        public static void GoBack()
        {
            if (!CanGoBack) return;
            currentIndex--;
            Select(history[currentIndex]);
        }

        public static void GoForward()
        {
            if (!CanGoForward) return;
            currentIndex++;
            Select(history[currentIndex]);
        }

        static void Select(GameObject go)
        {
            if (go == null) return;

            suppressTracking = true;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            suppressTracking = false;
        }
    }
}
#endif
