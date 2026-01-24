using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using FeatureAggregator;

namespace UnityProductivityTools.Dashboard
{
    public class UnityToolsDashboard : EditorWindow
    {
        private Vector2 sidebarScroll;
        private string selectedTool = "Welcome";
        private Dictionary<string, System.Action> tools;
        private List<string> toolOrder;

        // Tool UIs
        private FeatureAggregatorUI featureAggregatorUI;
        private UnityProductivityTools.TaskTool.Editor.TaskManagerUI taskManagerUI;
        private UnityProductivityTools.Bootstrapper.Editor.BootstrapperUI bootstrapperUI;
        private GameDevTools.Editor.TodoScannerUI todoScannerUI;
        private UnityTools.Editor.AssetSyncTool.AssetSyncUI assetSyncUI;
        private UnityProductivityTools.Terminal.TerminalUI terminalUI;
        private GameDevTools.Editor.NoteDashboardUI noteUI;
        private HiddenDeps.HiddenDependencyDetectorUI dependencyUI;
        private UnityTools.ObjectComparison.ComparisonUI comparisonUI;
        private UnityTools.ObjectGrouper.UI.ObjectGrouperUI grouperUI;
        private UnityProductivityTools.SnapshotTool.SnapshotUI snapshotUI;
        private UnityProductivityTools.AdvancedInspector.AdvancedInspectorUI inspectorUI;
        private UnityProductivityTools.TaskTool.Editor.TaskManagerSyncedUI taskManagerSyncedUI;
        private UnityProductivityTools.CodeEditor.CodeEditorUI codeEditorUI;
        private UnityProductivityTools.GoogleSheet.Editor.GSheetDataViewerUI gsheetUI;
        private DashboardWelcomeUI welcomeUI;

        [MenuItem("Tools/GameDevTools/Unified Dashboard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityToolsDashboard>("Unity Dashboard");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            InitializeTools();
        }

        private void InitializeTools()
        {
            // Initialize Feature Aggregator
            if (featureAggregatorUI == null)
            {
                featureAggregatorUI = new FeatureAggregatorUI();
                featureAggregatorUI.Initialize(this);
            }

            // Initialize Task Manager
            if (taskManagerUI == null)
            {
                taskManagerUI = new UnityProductivityTools.TaskTool.Editor.TaskManagerUI();
                taskManagerUI.Initialize();
            }

            // Initialize Bootstrapper
            if (bootstrapperUI == null)
            {
                bootstrapperUI = new UnityProductivityTools.Bootstrapper.Editor.BootstrapperUI();
            }

            // Initialize Todo Scanner
            if (todoScannerUI == null)
            {
                todoScannerUI = new GameDevTools.Editor.TodoScannerUI();
                todoScannerUI.Initialize();
            }

            // Initialize Asset Sync
            if (assetSyncUI == null)
            {
                assetSyncUI = new UnityTools.Editor.AssetSyncTool.AssetSyncUI();
            }

            // Initialize Terminal
            if (terminalUI == null)
            {
                terminalUI = new UnityProductivityTools.Terminal.TerminalUI();
                terminalUI.Initialize();
            }

            // Initialize Note Dashboard
            if (noteUI == null)
            {
                noteUI = new GameDevTools.Editor.NoteDashboardUI();
                noteUI.Initialize();
            }

            // Initialize Hidden Dependency Detector
            if (dependencyUI == null)
            {
                dependencyUI = new HiddenDeps.HiddenDependencyDetectorUI();
                dependencyUI.Initialize();
            }

            // Initialize Object Comparison
            if (comparisonUI == null)
            {
                comparisonUI = new UnityTools.ObjectComparison.ComparisonUI();
                comparisonUI.Initialize();
            }

            // Initialize Object Grouper
            if (grouperUI == null)
            {
                grouperUI = new UnityTools.ObjectGrouper.UI.ObjectGrouperUI();
                grouperUI.Initialize();
            }

            // Initialize Snapshot Tool
            if (snapshotUI == null)
            {
                snapshotUI = new UnityProductivityTools.SnapshotTool.SnapshotUI();
                snapshotUI.Initialize();
            }

            // Initialize Advanced Inspector
            if (inspectorUI == null)
            {
                inspectorUI = new UnityProductivityTools.AdvancedInspector.AdvancedInspectorUI();
                inspectorUI.Initialize();
            }

            // Initialize Task Manager (Synced)
            if (taskManagerSyncedUI == null)
            {
                taskManagerSyncedUI = new UnityProductivityTools.TaskTool.Editor.TaskManagerSyncedUI();
                taskManagerSyncedUI.Initialize();
            }

            // Initialize Welcome
            if (welcomeUI == null)
            {
                welcomeUI = new DashboardWelcomeUI();
            }

            // Initialize Code Editor
            if (codeEditorUI == null)
            {
                codeEditorUI = new UnityProductivityTools.CodeEditor.CodeEditorUI();
                codeEditorUI.Initialize();
            }

            // Initialize GSheet Data Viewer
            if (gsheetUI == null)
            {
                gsheetUI = new UnityProductivityTools.GoogleSheet.Editor.GSheetDataViewerUI();
                gsheetUI.Initialize();
            }

            // Define tools map
            tools = new Dictionary<string, System.Action>
            {
                { "Welcome", DrawWelcome },
                { "Feature Aggregator", DrawFeatureAggregator },
                { "Task Manager", DrawTaskManager },
                { "Task Manager (Synced)", DrawTaskManagerSynced },
                { "Project Bootstrapper", DrawBootstrapper },
                { "TODO Scanner", DrawTodoScanner },
                { "Asset Sync", DrawAssetSync },
                { "Integrated Terminal", DrawTerminal },
                { "Note Dashboard", DrawNoteDashboard },
                { "Hidden Dependency Detector", DrawHiddenDependencyDetector },
                { "Object Comparison", DrawObjectComparison },
                { "Object Grouper", DrawObjectGrouper },
                { "Snapshot Manager", DrawSnapshotTool },
                { "Advanced Inspector", DrawAdvancedInspector },
                { "Code Editor", DrawCodeEditor },
                { "GSheet Data Viewer", DrawGSheetDataViewer },
            };

            toolOrder = new List<string>(tools.Keys);
        }
        
        private void OnFocus()
        {
            // Propagate focus events if needed
            if (selectedTool == "Feature Aggregator") featureAggregatorUI?.OnFocus();
            if (selectedTool == "Note Dashboard") noteUI?.OnFocus();
        }

        private void OnDisable()
        {
            gsheetUI?.OnDisable();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            // Update terminal (real-time output)
            if (terminalUI != null)
            {
                terminalUI.Update();
                if (selectedTool == "Integrated Terminal") Repaint();
            }

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            DrawSidebar();

            // Main Content Area
            EditorGUILayout.BeginVertical();
            
            // Sub-header
            DrawHeader();

            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
            DrawMainContent();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(40));
            EditorGUI.DrawRect(headerRect, DashboardStyle.HeaderColor);
            
            GUILayout.Space(20);
            GUILayout.Label(selectedTool.ToUpper(), new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 14,
                normal = { textColor = DashboardStyle.AccentColor },
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 40
            });

            GUILayout.FlexibleSpace();
            
            if (GUILayout.Toggle(false, "v1.0.0", "MiniLabel", GUILayout.Height(40))) { }
            GUILayout.Space(10);

            EditorGUILayout.EndHorizontal();
            DashboardStyle.DrawLine();
        }

        private void DrawSidebar()
        {
            Rect sidebarRect = EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUI.DrawRect(sidebarRect, DashboardStyle.SidebarColor);
            
            // Logo / Title area
            EditorGUILayout.BeginVertical(GUILayout.Height(60));
            GUILayout.Space(15);
            GUILayout.Label("  UNITY TOOLS", new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                normal = { textColor = Color.white }
            });
            EditorGUILayout.EndVertical();
            
            DashboardStyle.DrawLine();

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

            GUILayout.Space(10);

            foreach (var toolName in toolOrder)
            {
                if (GUILayout.Button(toolName, selectedTool == toolName ? DashboardStyle.SidebarButtonSelected : DashboardStyle.SidebarButton))
                {
                    selectedTool = toolName;
                }
            }

            EditorGUILayout.EndScrollView();
            
            GUILayout.FlexibleSpace();
            DashboardStyle.DrawLine();
            GUILayout.Label("PRO SUITE", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainContent()
        {
            if (tools.ContainsKey(selectedTool))
            {
                tools[selectedTool]?.Invoke();
            }
            else
            {
                GUILayout.Label("Select a tool from the sidebar.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawFeatureAggregator()
        {
            if (featureAggregatorUI == null) InitializeTools();
            featureAggregatorUI.Draw();
        }

        private void DrawTaskManager()
        {
            if (taskManagerUI == null) InitializeTools();
            taskManagerUI.Draw();
        }

        private void DrawBootstrapper()
        {
            if (bootstrapperUI == null) InitializeTools();
            bootstrapperUI.Draw();
        }

        private void DrawTodoScanner()
        {
            if (todoScannerUI == null) InitializeTools();
            todoScannerUI.Draw();
        }

        private void DrawAssetSync()
        {
            if (assetSyncUI == null) InitializeTools();
            assetSyncUI.Draw();
        }

        private void DrawTerminal()
        {
            if (terminalUI == null) InitializeTools();
            terminalUI.Draw(position);
        }

        private void DrawNoteDashboard()
        {
            if (noteUI == null) InitializeTools();
            noteUI.Draw();
        }

        private void DrawHiddenDependencyDetector()
        {
            if (dependencyUI == null) InitializeTools();
            dependencyUI.Draw();
        }

        private void DrawObjectComparison()
        {
            if (comparisonUI == null) InitializeTools();
            comparisonUI.Draw(position);
        }

        private void DrawObjectGrouper()
        {
            if (grouperUI == null) InitializeTools();
            grouperUI.Draw();
        }

        private void DrawSnapshotTool()
        {
            if (snapshotUI == null) InitializeTools();
            snapshotUI.Draw();
        }

        private void DrawAdvancedInspector()
        {
            if (inspectorUI == null) InitializeTools();
            inspectorUI.UpdateSelection();
            inspectorUI.Draw(position);
        }

        private void DrawTaskManagerSynced()
        {
            if (taskManagerSyncedUI == null) InitializeTools();
            taskManagerSyncedUI.Draw();
        }

        private void DrawWelcome()
        {
            if (welcomeUI == null) InitializeTools();
            welcomeUI.Draw();
        }

        private void DrawCodeEditor()
        {
            if (codeEditorUI == null) InitializeTools();
            codeEditorUI.Draw();
        }

        private void DrawGSheetDataViewer()
        {
            if (gsheetUI == null) InitializeTools();
            gsheetUI.Draw();
        }

    }
}
