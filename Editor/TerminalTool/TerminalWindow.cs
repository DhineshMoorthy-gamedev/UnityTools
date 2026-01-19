using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace UnityProductivityTools.Terminal
{
    public class TerminalWindow : EditorWindow
    {
        private List<TerminalSession> sessions = new List<TerminalSession>();
        private int selectedTab = 0;
        private Vector2 scrollPos;
        private string commandInput = "";
        private GUIStyle outputStyle;
        private GUIStyle inputStyle;
        private Font monospaceFont;

        [MenuItem("Tools/GameDevTools/Integrated Terminal %`")]
        public static void ShowWindow()
        {
            var window = GetWindow<TerminalWindow>("Terminal");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            if (sessions.Count == 0)
            {
                CreateNewSession();
            }
            else
            {
                // Domain reload happened - restart processes that were running
                foreach (var session in sessions)
                {
                    if (session.WasRunning)
                    {
                        session.Start();
                    }
                }
            }
            
            // Try to find a monospace font
            monospaceFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Courier", "monospace" }, 12);
        }

        private void OnDisable()
        {
            foreach (var session in sessions)
            {
                if (session.IsRunning)
                {
                    session.ReloadPersistence();
                    session.Stop();
                    // Mark as was running so it restarts
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
            Repaint();
        }

        private void OnGUI()
        {
            // Handle keyboard shortcuts first
            HandleKeyboard();

            InitStyles();

            DrawTabBar();
            DrawToolbar();
            DrawOutputArea();
            DrawInputField();

            // Always try to focus the input if nothing else is focused
            if (string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()) || GUI.GetNameOfFocusedControl() == "TerminalInput")
            {
                GUI.FocusControl("TerminalInput");
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
                inputStyle.padding = new RectOffset(8, 8, 8, 8);
                inputStyle.fixedHeight = 35;
            }
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            int tabToClose = -1;
            
            for (int i = 0; i < sessions.Count; i++)
            {
                bool isSelected = (selectedTab == i);
                string label = sessions[i].SessionName;
                
                if (GUILayout.Toggle(isSelected, label, EditorStyles.toolbarButton, GUILayout.MaxWidth(150)))
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
            
            // Close tab after GUI layout is complete
            if (tabToClose != -1)
            {
                CloseSession(tabToClose);
            }
        }

        private void CloseSession(int index)
        {
            sessions[index].Stop();
            sessions.RemoveAt(index);
            if (selectedTab >= sessions.Count)
            {
                selectedTab = Mathf.Max(0, sessions.Count - 1);
            }
            if (sessions.Count == 0)
            {
                CreateNewSession();
            }
        }

        private void DrawToolbar()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            session.Shell = (ShellType)EditorGUILayout.EnumPopup(session.Shell, EditorStyles.toolbarDropDown, GUILayout.Width(100));

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                session.Clear();
            }

            if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                session.Stop();
                session.AppendToOutput("Process stopped manually.");
            }

            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                session.Restart();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private bool scrollToBottom = false;

        private void DrawOutputArea()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            
            // Use SelectableLabel to allow copying data by dragging
            float height = outputStyle.CalcHeight(new GUIContent(session.Output), position.width - 35);
            EditorGUILayout.SelectableLabel(session.Output, outputStyle, GUILayout.MinHeight(height), GUILayout.ExpandHeight(true));

            if (scrollToBottom)
            {
                scrollPos.y = float.MaxValue;
                scrollToBottom = false;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawInputField()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            EditorGUILayout.BeginHorizontal();
            
            // Show current directory in prompt
            string prompt = session.CurrentDirectory + " >";
            GUIStyle promptStyle = new GUIStyle(EditorStyles.label);
            promptStyle.font = monospaceFont;
            promptStyle.fontSize = 12;
            promptStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.8f, 1f) : new Color(0f, 0.3f, 0.6f);
            
            GUILayout.Label(prompt, promptStyle, GUILayout.Height(35));
            
            // Detect Enter key before it's consumed by the TextField
            Event e = Event.current;
            bool enterPressed = false;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "TerminalInput")
            {
                enterPressed = true;
                e.Use();
            }

            GUI.SetNextControlName("TerminalInput");
            commandInput = EditorGUILayout.TextField(commandInput, inputStyle);
            
            if (enterPressed || GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(35)))
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
            
            // Ensure focus stays on input if window is clicked
            if (Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl("TerminalInput");
            }
        }

        private void HandleKeyboard()
        {
            if (selectedTab < 0 || selectedTab >= sessions.Count) return;
            var session = sessions[selectedTab];

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // Ctrl+C to interrupt
                if (e.control && e.keyCode == KeyCode.C)
                {
                    session.SendControlC();
                    e.Use();
                    Repaint();
                }
                // Handle Escape to prevent focus loss
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

        private void Update()
        {
            bool needsRepaint = false;
            foreach (var session in sessions)
            {
                int oldLen = session.Output.Length;
                session.Update();
                if (session.Output.Length != oldLen)
                {
                    needsRepaint = true;
                    if (sessions.IndexOf(session) == selectedTab)
                    {
                        scrollToBottom = true;
                    }
                }
            }

            if (needsRepaint)
            {
                Repaint();
            }
        }
    }
}
