using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Reflection;

namespace UnityProductivityTools.AdvancedInspector
{
    public class ScriptEditorWindow : EditorWindow
    {
        private Component targetComponent;
        private string scriptContent;
        private Vector2 scrollPos;
        private MonoScript script;
        private GUIStyle editorTextStyle;
        private GUIStyle highlightedTextStyle;
        private GUIStyle lineNuStyle;
        
        private int fontSize = 12;
        private bool showSearch = false;
        private bool showReplace = false;
        private string searchText = "";
        private string replaceText = "";
        private int currentMatchIndex = -1;
        private int matchCount = 0;
        
        private const int LINE_NUMBER_WIDTH = 30; // Reduced width
        private const float SEARCH_HEIGHT = 20;

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
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            // Background Layer (Highlighted Text) - Keeps the background box appearance
            highlightedTextStyle = new GUIStyle(EditorStyles.textArea);
            highlightedTextStyle.wordWrap = false;
            highlightedTextStyle.richText = true;
            
            // Foreground Layer (Input) - Transparent background, Transparent text (cursor visible)
            editorTextStyle = new GUIStyle(EditorStyles.textArea);
            editorTextStyle.wordWrap = false;
            editorTextStyle.richText = false;
            
            // Make foreground transparent so background shows through
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
            
            // Try to use a decent monospace font
            Font font = null;
            try { font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize); } catch {}
            if (font == null) try { font = Font.CreateDynamicFontFromOSFont("Courier New", fontSize); } catch {}
            
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
            if (targetComponent == null)
            {
                EditorGUILayout.HelpBox("No component selected.", MessageType.Info);
                return;
            }
            
            if (editorTextStyle == null || editorTextStyle.font == null) InitializeStyles();

            // Keyboard & Mouse Shortcuts
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.control && e.keyCode == KeyCode.F)
                {
                    showSearch = !showSearch;
                    if (showSearch) 
                    {
                        // Defer focus to make sure the control exists in layout
                         EditorGUI.FocusTextInControl("SearchField");
                         focusedSearch = true; // Use flag to force focus in Repaint if needed
                    }
                    else
                    {
                         GUI.FocusControl(null);
                    }
                    e.Use();
                }
                
                // Toggle Replace (Ctrl+H)
                if (e.control && e.keyCode == KeyCode.H)
                {
                    showSearch = true;
                    showReplace = !showReplace;
                    e.Use();
                }
                
                // Font Size Scaling (Keys)
                // Note: Standard plus key is often KeyCode.Equals (Shift+= is +)
                // We check for KeypadPlus, Plus, and Equals to cover all bases
                if (e.control && (e.keyCode == KeyCode.KeypadPlus || e.keyCode == KeyCode.Plus || e.keyCode == KeyCode.Equals))
                {
                    ChangeFontSize(1);
                    e.Use();
                }
                if (e.control && (e.keyCode == KeyCode.KeypadMinus || e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadDivide)) // Just in case
                {
                   ChangeFontSize(-1);
                   e.Use();
                }
            }
            // Font Size Scaling (Scroll)
            else if (e.type == EventType.ScrollWheel && e.control)
            {
                // delta is usually 3 or -3 depending on OS settings, but sign matters
                if (e.delta.y < 0) ChangeFontSize(1); // Scroll Up = Zoom In
                else if (e.delta.y > 0) ChangeFontSize(-1); // Scroll Down = Zoom Out
                e.Use();
            }

            // Header
            DrawToolbar();
            
            // Handle delayed focus
            if (focusedSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("SearchField");
                focusedSearch = false; 
            }

            // Search Bar
            if (showSearch)
            {
                DrawSearchBar();
            }

            // Main Editor Area
            DrawEditorArea();

            // Footer
            DrawFooter();
            
            // Overlays
            DrawHelp();
        }

        private bool focusedSearch = false;
        
        private void ChangeFontSize(int delta)
        {
             fontSize = Mathf.Clamp(fontSize + delta, 8, 36);
             InitializeStyles();
             Repaint();
        }

        private bool showHelp = false;

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Editing: {targetComponent.GetType().Name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            // Help Button
            if (GUILayout.Button(EditorGUIUtility.IconContent("_Help"), EditorStyles.toolbarButton))
            {
                showHelp = !showHelp;
            }
            
            GUILayout.Label($"Line: {GetLineCount()} | Size: {fontSize}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawHelp()
        {
            if (!showHelp) return;
            
            // Calculate dynamic Y offset to avoid covering Search/Replace bars
            float topOffset = 22; // Height of main toolbar + padding
            if (showSearch) topOffset += 22;
            if (showReplace) topOffset += 22;
            
            Rect helpRect = new Rect(position.width - 260, topOffset, 250, 160);
            
            // Draw a shadow/box background
            GUI.Box(helpRect, "", EditorStyles.helpBox);
            
            GUILayout.BeginArea(helpRect);
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ctrl + F", "Find");
            EditorGUILayout.LabelField("Ctrl + H", "Replace");
            EditorGUILayout.LabelField("Ctrl + Scroll", "Zoom Text");
            EditorGUILayout.LabelField("Ctrl+ +/-", "Zoom Text");
            EditorGUILayout.LabelField("Click 'Save & Compile'", "Apply Changes");
            GUILayout.EndArea();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginVertical();
            
            // Search Row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Toggle Replace Button
            string arrow = showReplace ? "▼" : "▶";
            if (GUILayout.Button(arrow, EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                showReplace = !showReplace;
            }
            
            GUI.SetNextControlName("SearchField");
            
            EditorGUI.BeginChangeCheck();
            string oldSearch = searchText;
            searchText = EditorGUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSearch();
                // If typed something new, jump to first match?
                if (searchText != oldSearch && matchCount > 0)
                {
                    currentMatchIndex = -1;
                    NextMatch(false);
                }
            }

            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                searchText = "";
                UpdateSearch();
                GUI.FocusControl(null);
            }

            if (matchCount > 0)
            {
                if (GUILayout.Button("Find Next", EditorStyles.toolbarButton)) NextMatch(true);
                GUILayout.Label($"{currentMatchIndex + 1}/{matchCount}", EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(searchText))
            {
                 GUILayout.Label("No matches", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", EditorStyles.toolbarButton)) showSearch = false;
            EditorGUILayout.EndHorizontal();
            
            // Replace Row
            if (showReplace)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Space(25); // Indent to align with search box
                replaceText = EditorGUILayout.TextField(replaceText, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(200));
                
                if (GUILayout.Button("Replace", EditorStyles.toolbarButton))
                {
                    ReplaceNext();
                }
                if (GUILayout.Button("Replace All", EditorStyles.toolbarButton))
                {
                    ReplaceAll();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawEditorArea()
        {
            // Calculate line numbers string
            int lineCount = GetLineCount();
            System.Text.StringBuilder lineNumbers = new System.Text.StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                lineNumbers.AppendLine(i.ToString());
            }

            EditorGUILayout.BeginHorizontal(); 

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.BeginHorizontal();

            // Gutter (Line Numbers)
            float lineHeight = editorTextStyle.lineHeight;
            float minHeight = lineCount * lineHeight + 50; 
            
            GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox);
            boxStyle.margin = new RectOffset(0,0,0,0);
            
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(LINE_NUMBER_WIDTH));
            EditorGUILayout.LabelField(lineNumbers.ToString(), lineNuStyle, GUILayout.ExpandHeight(true), GUILayout.MinHeight(minHeight));
            EditorGUILayout.EndVertical();

            // Text Area Container
            Rect editorRect = EditorGUILayout.BeginVertical();

            // 1. Draw Highlighted Text (Background)
            string highlighted = ApplySyntaxHighlighting(scriptContent);
            if (showSearch && !string.IsNullOrEmpty(searchText))
            {
                highlighted = ApplySearchHighlighting(highlighted, searchText, currentMatchIndex);
            }
            
            // Get rect for the editor
            Rect textRect = EditorGUILayout.GetControlRect(false, GUILayout.MinHeight(minHeight), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            // Draw Background Highlighted
            GUI.Label(textRect, highlighted, highlightedTextStyle);
            
            // INTERCEPTION STRATEGY:
            // If the editor is NOT focused, we draw an invisible button over it.
            // If the user clicks this button, we manually focus and position the cursor.
            // This bypasses Unity's built-in "Select All on Focus" behavior.
            // checking !showGoToLine prevents this button from stealing clicks meant for the overlay
            if (GUI.GetNameOfFocusedControl() != "ScriptTextArea")
            {
                if (GUI.Button(textRect, GUIContent.none, GUIStyle.none))
                {
                    GUI.FocusControl("ScriptTextArea");
                    lastMousePosition = Event.current.mousePosition;
                    requestMoveCursorToMouse = true;
                    // We don't need to repaint immediately, the Repaint event will pick up the request
                }
            }
            
            // Draw Foreground Editor
            GUI.SetNextControlName("ScriptTextArea");
            EditorGUI.BeginChangeCheck();
            
            string newContent = EditorGUI.TextArea(textRect, scriptContent, editorTextStyle);
            
            if (EditorGUI.EndChangeCheck())
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.character == '\n')
                {
                    newContent = HandleAutoIndent(scriptContent, newContent);
                }
                newContent = newContent.Replace("\t", "    "); // Tabs to spaces
                scriptContent = newContent;
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
            
            // Handle Manual Scroll to Match
            if (scrollToLine >= 0 && Event.current.type == EventType.Repaint)
            {
                 float targetY = scrollToLine * editorTextStyle.lineHeight;
                 scrollPos.y = Mathf.Max(0, targetY - (position.height / 2));
                 scrollToLine = -1;
                 Repaint();
            }
            
            // Handle Pending Selection (Search Match or Click Fix)
            if (Event.current.type == EventType.Repaint)
            {
                if (pendingSelectionStart >= 0)
                {
                    TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (te != null)
                    {
                        te.cursorIndex = pendingSelectionStart;
                        te.selectIndex = pendingSelectionStart + pendingSelectionLength;
                        pendingSelectionStart = -1; // Reset
                        pendingSelectionLength = 0;
                        Repaint();
                    }
                }
                else if (requestMoveCursorToMouse)
                {
                    TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (te != null)
                    {
                        te.MoveCursorToPosition(lastMousePosition);
                        te.selectIndex = te.cursorIndex; // Clear selection
                        requestMoveCursorToMouse = false;
                        Repaint();
                    }
                }
            }
        }
        
        private Vector2 lastMousePosition;
        private bool requestMoveCursorToMouse = false;
        
        private string ApplySyntaxHighlighting(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            
            // Keywords
            string pattern = @"\b(public|private|protected|void|string|int|float|bool|class|namespace|using|return|new|if|else|for|foreach|while|null|true|false)\b";
            string colored = Regex.Replace(code, pattern, "<color=#569CD6>$1</color>");
            
            // Strings
            colored = Regex.Replace(colored, "\"[^\"]*\"", "<color=#D69D85>$0</color>");
            
            // Comments
            colored = Regex.Replace(colored, @"//.*", "<color=#57A64A>$0</color>");
            
            return colored;
        }

        private string ApplySearchHighlighting(string code, string search, int matchIdx)
        {
             return Regex.Replace(code, Regex.Escape(search), "<color=yellow>$0</color>", RegexOptions.IgnoreCase);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & Compile", GUILayout.Height(30)))
            {
                SaveScript();
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SaveScript()
        {
            if (script != null)
            {
                File.WriteAllText(AssetDatabase.GetAssetPath(script), scriptContent);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(script));
                Close();
            }
        }

        private int GetLineCount()
        {
            if (string.IsNullOrEmpty(scriptContent)) return 1;
            int count = 1;
            for (int i = 0; i < scriptContent.Length; i++)
            {
                if (scriptContent[i] == '\n') count++;
            }
            return count;
        }
        
        private string HandleAutoIndent(string oldContent, string newContent)
        {
             return newContent;
        }
        
        private void UpdateSearch()
        {
             if (string.IsNullOrEmpty(searchText))
            {
                matchCount = 0;
                currentMatchIndex = -1;
                return;
            }
             try {
                matchCount = Regex.Matches(scriptContent, Regex.Escape(searchText), RegexOptions.IgnoreCase).Count;
                if (currentMatchIndex >= matchCount) currentMatchIndex = 0;
            } catch { matchCount = 0; }
        }

        private int scrollToLine = -1;
        private int pendingSelectionStart = -1;
        private int pendingSelectionLength = 0;

        private void NextMatch(bool focusEditor = true)
        {
            if (matchCount == 0) return;
            currentMatchIndex = (currentMatchIndex + 1) % matchCount;
            
            // Find the match
            MatchCollection matches = Regex.Matches(scriptContent, Regex.Escape(searchText), RegexOptions.IgnoreCase);
             if (matches.Count > currentMatchIndex)
             {
                 var match = matches[currentMatchIndex];
                 
                 // Calculate line number
                 int lineIndex = 0;
                 for(int i = 0; i < match.Index && i < scriptContent.Length; i++)
                 {
                     if(scriptContent[i] == '\n') lineIndex++;
                 }
                 
                 scrollToLine = lineIndex;
                 
                 if (focusEditor) 
                 {
                     GUI.FocusControl("ScriptTextArea");
                     pendingSelectionStart = match.Index;
                     pendingSelectionLength = match.Length;
                 }
            }
        }
        
        // Remove old helper methods that are no longer used
        // private void FocusMatch(int index) ... 
        // private void ScrollToCaret() ... 

        private void ReplaceNext()
        {
            if (matchCount == 0 || string.IsNullOrEmpty(searchText)) return;
            
            // Re-find matches to ensure indices are fresh
            MatchCollection matches = Regex.Matches(scriptContent, Regex.Escape(searchText), RegexOptions.IgnoreCase);
            
            // If currentMatchIndex is invalid, try to find the first one
            if (currentMatchIndex < 0 || currentMatchIndex >= matches.Count)
            {
                currentMatchIndex = 0;
            }
            
            if (matches.Count > currentMatchIndex)
            {
                Match match = matches[currentMatchIndex];
                
                // Perform replacement
                // We use Remove and Insert instead of Regex.Replace on the whole string to target THIS specific match
                scriptContent = scriptContent.Remove(match.Index, match.Length).Insert(match.Index, replaceText);
                
                // Update search results
                UpdateSearch();
                
                // Move to next match automatically? Usually replacing keeps you at the same spot or moves next.
                // Since the text changed, indices shifted. UpdateSearch handles match count.
                // Let's try to stay at the same index (which is now the "next" occurrence relative to file start)
                // effectively acting like "move next" because the current one is gone/changed.
                 if (currentMatchIndex >= matchCount) currentMatchIndex = 0;
                 
                 // If there are still matches, highlight the next one
                 if (matchCount > 0)
                 {
                    NextMatch(false); // Don't steal focus, just move highlight
                 }
            }
        }
        
        private void ReplaceAll()
        {
             if (string.IsNullOrEmpty(searchText)) return;
             
             // Simple Regex Replace All
             string newContent = Regex.Replace(scriptContent, Regex.Escape(searchText), replaceText, RegexOptions.IgnoreCase);
             
             if (newContent != scriptContent)
             {
                 scriptContent = newContent;
                 UpdateSearch();
                 Repaint();
             }
        }
    }
}
