using UnityEditor;
using UnityEngine;
using System.IO;

namespace UnityProductivityTools.AdvancedInspector
{
    public class ScriptEditorWindow : EditorWindow
    {
        private Component targetComponent;
        private string scriptContent;
        private Vector2 scrollPos;
        private MonoScript script;
        private GUIStyle cachedTextStyle;

        public static void ShowWindow(Component comp, string initialContent)
        {
            var window = GetWindow<ScriptEditorWindow>("Script Editor");
            window.targetComponent = comp;
            window.scriptContent = initialContent;
            window.script = MonoScript.FromMonoBehaviour(comp as MonoBehaviour);
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            // Cache the text style on enable to avoid recreating it every frame
            cachedTextStyle = new GUIStyle(EditorStyles.textArea);
            cachedTextStyle.wordWrap = false;
            cachedTextStyle.richText = false;
            
            // Use a system monospace font
            try
            {
                cachedTextStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            }
            catch
            {
                // Fallback if Consolas is not available
                cachedTextStyle.font = Font.CreateDynamicFontFromOSFont("Courier New", 12);
            }
        }

        private void OnGUI()
        {
            if (targetComponent == null)
            {
                EditorGUILayout.HelpBox("No component selected.", MessageType.Info);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Editing: {targetComponent.GetType().Name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Script content with cached style
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            // Use cached style instead of creating new one each frame
            scriptContent = EditorGUILayout.TextArea(scriptContent, cachedTextStyle, GUILayout.ExpandHeight(true));
            
            EditorGUILayout.EndScrollView();

            // Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & Compile", GUILayout.Height(30)))
            {
                if (script != null)
                {
                    File.WriteAllText(AssetDatabase.GetAssetPath(script), scriptContent);
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(script));
                    Close();
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
