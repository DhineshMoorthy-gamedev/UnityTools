using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace UnityProductivityTools.Terminal
{
    [System.Serializable]
    public class TerminalUI
    {
        private List<TerminalSession> sessions = new List<TerminalSession>();
        private int selectedTab = 0;
        private Vector2 scrollPos;
        private string commandInput = "";
        private GUIStyle outputStyle;
        private GUIStyle inputStyle;
        private GUIStyle promptStyle;
        private Font monospaceFont;
        // Re-added position storage to handle SelectableLabel height calculations
        private Rect windowPosition;

        public void Initialize()
        {
            if (sessions.Count == 0)
            {
                CreateNewSession();
            }
            else
            {
                foreach (var session in sessions)
                {
                    if (session.WasRunning) session.Start();
                }
            }
            
            monospaceFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Courier", "monospace" }, 12);
        }

        public void OnDisable()
        {
            foreach (var session in sessions)
            {
                if (session.IsRunning)
                {
                    session.ReloadPersistence();
                    session.Stop();
                    session.WasRunning = true;
                }
            }
        }

        public void CreateNewSession(string workingDir = null)
        {
            if (string.IsNullOrEmpty(workingDir))
            {
                workingDir = Directory.GetCurrentDirectory();
            }

            string name = Path.GetFileName(workingDir);
            if (name == "UnityTools") name = "Project Root";

            var session = new TerminalSession(name, workingDir, ShellType.PowerShell);
            session.Start();
            sessions.Add(session);
            selectedTab = sessions.Count - 1;
        }

        public void Draw(Rect position)
        {
            windowPosition = position;
            HandleKeyboard();
            InitStyles();

            DrawTabBar();
            DrawToolbar();
            DrawOutputArea();
            DrawInputField();

            if (Event.current.type == EventType.Layout)
            {
                if (string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()) && GUIUtility.hotControl == 0)
                {
                    GUI.FocusControl("TerminalInput");
                }
            }
        }

        private void InitStyles()
        {
            if (outputStyle == null)
            {
                outputStyle = new GUIStyle(EditorStyles.label);
                outputStyle.font = monospaceFont;
                outputStyle.wordWrap = true;
                outputStyle.richText = true;
                outputStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            }

            if (inputStyle == null)
            {
                inputStyle = new GUIStyle(EditorStyles.textField);
                inputStyle.font = monospaceFont;
                inputStyle.fontSize = 14;
                inputStyle.padding = new RectOffset(10, 10, 0, 0);
                inputStyle.alignment = TextAnchor.MiddleLeft;
                inputStyle.fixedHeight = 35;
            }

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(EditorStyles.label);
                promptStyle.font = monospaceFont;
                promptStyle.fontSize = 12;
                promptStyle.alignment = TextAnchor.MiddleLeft;
                promptStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.8f, 1f) : new Color(0f, 0.3f, 0.6f);
            }
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int tabToClose = -1;
            
            for (int i = 0; i < sessions.Count; i++)
            {
                bool isSelected = (selectedTab == i);
                if (GUILayout.Toggle(isSelected, sessions[i].SessionName, EditorStyles.toolbarButton, GUILayout.MaxWidth(150)))
                {
                    selectedTab = i;
                }

                if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    tabToClose = i;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                CreateNewSession();
            }
            EditorGUILayout.EndHorizontal();
            
            if (tabToClose != -1) CloseSession(tabToClose);
        }

        private void CloseSession(int index)
        {
            sessions[index].Stop();
            sessions.RemoveAt(index);
            selectedTab = Mathf.Clamp(selectedTab, 0, sessions.Count - 1);
            if (sessions.Count == 0) CreateNewSession();
        }

        private void DrawToolbar()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            session.Shell = (ShellType)EditorGUILayout.EnumPopup(session.Shell, EditorStyles.toolbarDropDown, GUILayout.Width(100));

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50))) session.Clear();
            if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                session.Stop();
                session.AppendToOutput("Process stopped manually.");
            }
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(60))) session.Restart();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private bool scrollToBottom = false;

        private void DrawOutputArea()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            // Use GUILayoutUtility to get the actual available width for the current view
            Rect dynamicRect = EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, true); // Always show vertical, never horizontal
            
            float availableWidth = dynamicRect.width > 0 ? dynamicRect.width - 25 : windowPosition.width - 230; // Fallback to approx dashboard width
            float height = outputStyle.CalcHeight(new GUIContent(session.Output), availableWidth) + 20;

            GUI.SetNextControlName("TerminalOutput");
            EditorGUILayout.SelectableLabel(session.Output, outputStyle, GUILayout.MinHeight(height), GUILayout.Width(availableWidth));
            
            if (scrollToBottom)
            {
                scrollPos.y = float.MaxValue;
                scrollToBottom = false;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInputField()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            EditorGUILayout.BeginHorizontal(GUILayout.Height(35));
            
            // Limit prompt width and use ellipsis if path is too long
            string prompt = session.CurrentDirectory + " > ";
            GUIContent promptContent = new GUIContent(prompt);
            float promptWidth = Mathf.Min(promptStyle.CalcSize(promptContent).x, 300);
            GUILayout.Label(promptContent, promptStyle, GUILayout.Width(promptWidth), GUILayout.Height(35));
            
            Event e = Event.current;
            bool enterPressed = false;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "TerminalInput")
            {
                enterPressed = true;
                e.Use();
            }

            GUI.SetNextControlName("TerminalInput");
            // Use ExpandWidth instead of fixed styles to let it fill space
            commandInput = EditorGUILayout.TextField("", commandInput, inputStyle, GUILayout.ExpandWidth(true), GUILayout.Height(35));
            
            if (enterPressed || GUILayout.Button("Send", GUILayout.Width(70), GUILayout.Height(35)))
            {
                if (!string.IsNullOrEmpty(commandInput))
                {
                    session.ExecuteCommand(commandInput);
                    commandInput = "";
                    scrollToBottom = true;
                }
                GUI.FocusControl("TerminalInput");
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2); // Tiny bottom margin
        }

        private void HandleKeyboard()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.control && e.keyCode == KeyCode.C)
                {
                    bool inputFocused = (GUI.GetNameOfFocusedControl() == "TerminalInput");
                    TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (inputFocused && (te == null || !te.hasSelection))
                    {
                        session.SendControlC();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    e.Use();
                    GUI.FocusControl("TerminalInput");
                }
                else if (e.keyCode == KeyCode.UpArrow)
                {
                    commandInput = session.GetHistoryUp(commandInput);
                    MoveCursorToEnd();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    commandInput = session.GetHistoryDown();
                    MoveCursorToEnd();
                    e.Use();
                }
            }
        }

        private void MoveCursorToEnd()
        {
            GUI.FocusControl("TerminalInput");
            TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te != null)
            {
                te.cursorIndex = commandInput.Length;
                te.selectIndex = commandInput.Length;
            }
        }

        public void Update()
        {
            bool needsRepaint = false;
            foreach (var session in sessions)
            {
                int oldLen = session.Output.Length;
                session.Update();
                if (session.Output.Length != oldLen)
                {
                    needsRepaint = true;
                    if (sessions.IndexOf(session) == selectedTab) scrollToBottom = true;
                }
            }
        }
    }
}
