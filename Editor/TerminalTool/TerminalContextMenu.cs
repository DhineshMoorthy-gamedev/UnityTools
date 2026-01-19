using UnityEditor;
using UnityEngine;
using System.IO;

namespace UnityProductivityTools.Terminal
{
    public static class TerminalContextMenu
    {
        [MenuItem("Assets/Open in Terminal Here", false, 20)]
        private static void OpenInTerminal()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) return;

            string fullPath = Path.GetFullPath(path);
            
            // If it's a file, get its directory
            if (!Directory.Exists(fullPath))
            {
                fullPath = Path.GetDirectoryName(fullPath);
            }

            var window = EditorWindow.GetWindow<TerminalWindow>("Terminal");
            window.Show();
            window.CreateNewSession(fullPath);
        }

        [MenuItem("Assets/Open in Terminal Here", true)]
        private static bool OpenInTerminalValidate()
        {
            return Selection.activeObject != null;
        }
    }
}
