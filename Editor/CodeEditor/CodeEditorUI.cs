using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.CodeEditor
{
    [System.Serializable]
    public class CodeEditorUI
    {
        public void Initialize()
        {
            // No initialization needed
        }

        public void Draw()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(20);
            
            GUILayout.Label("Code Editor", EditorStyles.largeLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "The Code Editor is a full-featured standalone window with:\n\n" +
                "• Syntax highlighting for C# code\n" +
                "• Searchable file browser\n" +
                "• Find and Replace functionality\n" +
                "• Line numbers and code formatting\n\n" +
                "Due to its complex UI and state management, it works best as a separate window.",
                MessageType.Info
            );
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Open Code Editor Window", GUILayout.Height(40)))
            {
                EditorApplication.ExecuteMenuItem("Tools/GameDevTools/Code Editor");
            }
            
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }
    }
}
