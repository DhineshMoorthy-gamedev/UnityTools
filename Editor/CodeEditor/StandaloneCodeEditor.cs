using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityProductivityTools.CodeEditor
{
    public class StandaloneCodeEditor : EditorWindow
    {
        private List<MonoScript> allScripts = new List<MonoScript>();
        private List<MonoScript> filteredScripts = new List<MonoScript>();
        private MonoScript selectedScript;
        private string scriptContent = "";
        private string originalContent = "";
        private string searchString = "";
        
        private Vector2 sidebarScroll;
        private Vector2 editorScroll;
        
        private GUIStyle editorTextStyle;
        private GUIStyle highlightedTextStyle;
        private GUIStyle lineNuStyle;
        private int fontSize = 12;

        private bool showSearch = false;
        private bool showReplace = false;
        private string findText = "";
        private string replaceText = "";
        private int currentMatchIndex = -1;
        private int matchCount = 0;
        
        // Script source filter
        private enum ScriptSource { AssetsOnly, All }
        private ScriptSource scriptSource = ScriptSource.AssetsOnly;

        [MenuItem("Tools/GameDevTools/Code Editor", false, 101)]
        public static void ShowWindow()
        {
            var window = GetWindow<StandaloneCodeEditor>("Code Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        public static void OpenScript(MonoScript script)
        {
            var window = GetWindow<StandaloneCodeEditor>("Code Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
            window.LoadScript(script);
        }

        private void OnEnable()
        {
            RefreshScriptList();
            InitializeStyles();
        }

        private void RefreshScriptList()
        {
            allScripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(s => s != null)
                .Where(s => {
                    if (scriptSource == ScriptSource.AssetsOnly)
                    {
                        string path = AssetDatabase.GetAssetPath(s);
                        return path.StartsWith("Assets/");
                    }
                    return true;
                })
                .OrderBy(s => s.name)
                .ToList();
            UpdateFilteredList();
        }

        private void UpdateFilteredList()
        {
            if (string.IsNullOrEmpty(searchString))
            {
                filteredScripts = new List<MonoScript>(allScripts);
            }
            else
            {
                filteredScripts = allScripts.Where(s => s.name.ToLower().Contains(searchString.ToLower())).ToList();
            }
        }

        private void InitializeStyles()
        {
            highlightedTextStyle = new GUIStyle(EditorStyles.textArea);
            highlightedTextStyle.wordWrap = false;
            highlightedTextStyle.richText = true;

            editorTextStyle = new GUIStyle(EditorStyles.textArea);
            editorTextStyle.wordWrap = false;
            editorTextStyle.richText = false;

            Texture2D clearTex = new Texture2D(1, 1);
            clearTex.SetPixel(0, 0, Color.clear);
            clearTex.Apply();

            editorTextStyle.normal.background = clearTex;
            editorTextStyle.active.background = clearTex;
            editorTextStyle.focused.background = clearTex;
            editorTextStyle.hover.background = clearTex;

            editorTextStyle.normal.textColor = Color.clear;
            editorTextStyle.active.textColor = Color.clear;
            editorTextStyle.focused.textColor = Color.clear;
            editorTextStyle.hover.textColor = Color.clear;

            Font font = null;
            try { font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize); } catch { }
            if (font == null) try { font = Font.CreateDynamicFontFromOSFont("Courier New", fontSize); } catch { }

            if (font != null)
            {
                editorTextStyle.font = font;
                highlightedTextStyle.font = font;
            }

            editorTextStyle.fontSize = fontSize;
            highlightedTextStyle.fontSize = fontSize;

            lineNuStyle = new GUIStyle(EditorStyles.label);
            lineNuStyle.alignment = TextAnchor.UpperRight;
            lineNuStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            lineNuStyle.fontSize = fontSize;
            lineNuStyle.font = editorTextStyle.font;
            lineNuStyle.padding = new RectOffset(0, 5, 2, 0);
        }

        private void OnGUI()
        {
            if (editorTextStyle == null || editorTextStyle.font == null) InitializeStyles();

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            DrawSidebar();

            // Main Editor
            EditorGUILayout.BeginVertical();
            if (selectedScript != null)
            {
                DrawEditorToolbar();
                if (showSearch) DrawSearchBar();
                DrawEditorArea();
                DrawFooter();
            }
            else
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a script to start editing", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            HandleShortcuts();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(250));
            
            // Source filter
            EditorGUI.BeginChangeCheck();
            scriptSource = (ScriptSource)EditorGUILayout.EnumPopup(scriptSource, GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck()) RefreshScriptList();
            
            EditorGUILayout.Space(2);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            searchString = EditorGUILayout.TextField("", searchString, "SearchTextField");
            if (EditorGUI.EndChangeCheck()) UpdateFilteredList();
            
            if (GUILayout.Button(new GUIContent("↻", "Refresh script list"), GUILayout.Width(35))) RefreshScriptList();
            EditorGUILayout.EndHorizontal();

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
            foreach (var script in filteredScripts)
            {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                if (selectedScript == script) style.fontStyle = FontStyle.Bold;

                if (GUILayout.Button(script.name, style))
                {
                    LoadScript(script);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void LoadScript(MonoScript script)
        {
            selectedScript = script;
            scriptContent = script.text;
            originalContent = scriptContent;
            GUI.FocusControl(null);
            showSearch = false;
        }

        private void DrawEditorToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Editing: {selectedScript.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("External", EditorStyles.toolbarButton))
            {
                AssetDatabase.OpenAsset(selectedScript);
            }
            
            if (GUILayout.Button("Find", EditorStyles.toolbarButton))
            {
                showSearch = !showSearch;
            }
            
            GUILayout.Label($"Size: {fontSize}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            findText = EditorGUILayout.TextField("Find", findText);
            if (EditorGUI.EndChangeCheck()) UpdateSearch();

            if (matchCount > 0)
            {
                if (GUILayout.Button("Next", GUILayout.Width(50))) NextMatch();
                GUILayout.Label($"{currentMatchIndex + 1}/{matchCount}", GUILayout.Width(50));
            }
            EditorGUILayout.EndHorizontal();

            if (showReplace)
            {
                EditorGUILayout.BeginHorizontal();
                replaceText = EditorGUILayout.TextField("Replace", replaceText);
                if (GUILayout.Button("Replace", GUILayout.Width(60))) ReplaceNext();
                if (GUILayout.Button("All", GUILayout.Width(40))) ReplaceAll();
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(showReplace ? "Hide Replace" : "Show Replace", EditorStyles.miniButton))
            {
                showReplace = !showReplace;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawEditorArea()
        {
            int lineCount = scriptContent.Split('\n').Length;
            
            // Build line numbers with exact same line breaks as the code
            System.Text.StringBuilder lineNumbers = new System.Text.StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                lineNumbers.Append(i.ToString());
                if (i < lineCount) lineNumbers.Append('\n'); // Match code's line breaks exactly
            }

            editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
            EditorGUILayout.BeginHorizontal();

            float minHeight = Mathf.Max(position.height - 150, lineCount * editorTextStyle.lineHeight + 50);

            // Calculate dynamic gutter width - more compact
            int digits = lineCount.ToString().Length;
            float gutterWidth = Mathf.Max(30, digits * 8 + 10);

            // Gutter
            Rect gutterRect = EditorGUILayout.BeginVertical(GUILayout.Width(gutterWidth));
            EditorGUI.DrawRect(new Rect(gutterRect.x, gutterRect.y, gutterWidth, minHeight), new Color(0.15f, 0.15f, 0.15f));
            
            // Match line numbers style exactly to code editor
            GUIStyle numStyle = new GUIStyle(lineNuStyle);
            numStyle.padding = new RectOffset(0, 5, 2, 0); // 5px from right edge of gutter
            
            EditorGUILayout.LabelField(lineNumbers.ToString(), numStyle, GUILayout.MinHeight(minHeight));
            EditorGUILayout.EndVertical();

            // No extra space here, let the text area handle its own padding
            Rect textRect = EditorGUILayout.BeginVertical();
            
            // Add a small left margin to the text area to separate from gutter
            GUIStyle editorContainerStyle = new GUIStyle();
            editorContainerStyle.padding = new RectOffset(5, 0, 0, 0); 
            EditorGUILayout.BeginVertical(editorContainerStyle);
            string highlighted = ApplySyntaxHighlighting(scriptContent);
            if (showSearch && !string.IsNullOrEmpty(findText))
            {
                 highlighted = Regex.Replace(highlighted, Regex.Escape(findText), "<color=yellow>$0</color>", RegexOptions.IgnoreCase);
            }

            Rect controlRect = EditorGUILayout.GetControlRect(false, GUILayout.MinHeight(minHeight));
            GUI.Label(controlRect, highlighted, highlightedTextStyle);

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("MainTextArea");
            string newContent = EditorGUI.TextArea(controlRect, scriptContent, editorTextStyle);
            if (EditorGUI.EndChangeCheck())
            {
                scriptContent = newContent.Replace("\t", "    ");
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private string ApplySyntaxHighlighting(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            string pattern = @"\b(public|private|protected|void|string|int|float|bool|class|namespace|using|return|new|if|else|for|foreach|while|null|true|false)\b";
            string colored = Regex.Replace(code, pattern, "<color=#569CD6>$1</color>");
            colored = Regex.Replace(colored, "\"[^\"]*\"", "<color=#D69D85>$0</color>");
            colored = Regex.Replace(colored, @"//.*", "<color=#57A64A>$0</color>");
            return colored;
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            bool hasChanges = scriptContent != originalContent;
            EditorGUI.BeginDisabledGroup(!hasChanges);
            if (GUILayout.Button("Save & Compile", GUILayout.Height(30)))
            {
                SaveScript();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Revert", GUILayout.Height(30)))
            {
                scriptContent = originalContent;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SaveScript()
        {
            string path = AssetDatabase.GetAssetPath(selectedScript);
            File.WriteAllText(path, scriptContent);
            AssetDatabase.ImportAsset(path);
            originalContent = scriptContent;
        }

        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control)
            {
                if (e.keyCode == KeyCode.F)
                {
                    showSearch = !showSearch;
                    e.Use();
                }
                if (e.keyCode == KeyCode.S)
                {
                    if (selectedScript != null && scriptContent != originalContent) SaveScript();
                    e.Use();
                }
            }
        }

        private void UpdateSearch()
        {
            if (string.IsNullOrEmpty(findText)) { matchCount = 0; return; }
            matchCount = Regex.Matches(scriptContent, Regex.Escape(findText), RegexOptions.IgnoreCase).Count;
            if (currentMatchIndex >= matchCount) currentMatchIndex = 0;
        }

        private void NextMatch()
        {
            if (matchCount == 0) return;
            currentMatchIndex = (currentMatchIndex + 1) % matchCount;
            // Scroll to logic could be added here
        }

        private void ReplaceNext()
        {
            if (matchCount == 0) return;
            Match m = Regex.Matches(scriptContent, Regex.Escape(findText), RegexOptions.IgnoreCase)[0];
            scriptContent = scriptContent.Remove(m.Index, m.Length).Insert(m.Index, replaceText);
            UpdateSearch();
        }

        private void ReplaceAll()
        {
            scriptContent = Regex.Replace(scriptContent, Regex.Escape(findText), replaceText, RegexOptions.IgnoreCase);
            UpdateSearch();
        }
    }
}
