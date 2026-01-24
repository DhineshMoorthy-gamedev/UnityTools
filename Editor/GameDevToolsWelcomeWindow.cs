using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools
{
    public class GameDevToolsWelcomeWindow : EditorWindow
    {
        private Vector2 scrollPos;
        
        // Foldout states
        private bool workflowFoldout = true;
        private bool sceneFoldout = true;
        private bool assetsFoldout = true;

        [MenuItem("Tools/GameDevTools/Welcome", false, 1000)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameDevToolsWelcomeWindow>("Tools Welcome");
            window.minSize = new Vector2(400, 600);
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space();
            GUILayout.Label("Game Dev Tools", EditorStyles.boldLabel);
            GUILayout.Label("Productivity & Workflow Suite", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Description
            EditorGUILayout.HelpBox("Explore our tools grouped by category. Click on a category to expand/collapse.", MessageType.Info);
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // 1. Workflow & Productivity
            workflowFoldout = EditorGUILayout.Foldout(workflowFoldout, "1. Workflow & Productivity", true, EditorStyles.foldoutHeader);
            if (workflowFoldout)
            {
                EditorGUI.indentLevel++;
                
                DrawToolItem(
                    "1.1 Feature Aggregator",
                    "Organize scripts and assets by feature/concept. One-click access to all related files with drag & drop support.",
                    "Tools/GameDevTools/Feature Aggregator"
                );

                DrawToolItem(
                    "1.2 Project Bootstrapper",
                    "Initialize new projects with standard folders, base scripts (Singleton, ObjectPool), and scenes in seconds.",
                    "Tools/GameDevTools/Project Bootstrapper"
                );

                DrawToolItem(
                    "1.3 Task Manager",
                    "Project-wide task tracking tool. Assign priorities, owners, and statuses to keep track of your work.",
                    "Tools/GameDevTools/Task Manager"
                );

                DrawToolItem(
                    "1.4 Task Manager (Synced)",
                    "Real-time synchronized task manager using WebSockets. Broadcasts task updates.",
                    "Tools/GameDevTools/Task Manager (Synced)"
                );

                DrawToolItem(
                    "1.5 TODO Scanner",
                    "Automatically scans your C# scripts for //TODO and //FIXME comments. Click to jump to the exact line.",
                    "Tools/GameDevTools/TODO Scanner"
                );
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // 2. Scene & Hierarchy
            sceneFoldout = EditorGUILayout.Foldout(sceneFoldout, "2. Scene & Hierarchy", true, EditorStyles.foldoutHeader);
            if (sceneFoldout)
            {
                EditorGUI.indentLevel++;

                DrawToolItem(
                    "2.1 Add Scene to Build", 
                    "Quickly add the selected scene asset to the Build Settings. (Right-click on Scene Asset in Project window > Add to Build Settings)",
                    null // Context Menu
                );

                DrawToolItem(
                    "2.2 Hierarchy Icons", 
                    "Auto-displays icons in the Hierarchy view for GameObjects with specific components.",
                    null // Passive
                );

                DrawToolItem(
                    "2.3 Object Comparison", 
                    "Deeply compare two GameObjects, their components, and hierarchy. Includes interactive syncing and history tracking.",
                    "Tools/GameDevTools/Object Comparison"
                );

                DrawToolItem(
                    "2.4 Object Grouper",
                    "Organize objects into logical groups without modifying the Hierarchy. Supports bulk visibility, locking, and selection.",
                    "Tools/GameDevTools/Object Grouper"
                );

                DrawToolItem(
                    "2.5 Snapshot Manager", 
                    "Capture, View, and Restore GameObject states (Transforms & Component values). Includes undo support.",
                    "Tools/GameDevTools/Snapshot Manager"
                );

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // 3. Asset Utilities
            assetsFoldout = EditorGUILayout.Foldout(assetsFoldout, "3. Asset Utilities", true, EditorStyles.foldoutHeader);
            if (assetsFoldout)
            {
                EditorGUI.indentLevel++;

                DrawToolItem(
                    "3.1 Advanced Inspector",
                    "Enhanced inspector with favorites, search, bulk editing, and built-in script editor with inline and maximized modes.",
                    "Tools/GameDevTools/Advanced Inspector"
                );

                DrawToolItem(
                    "3.2 Asset Sync Tool",
                    "Mark files/folders to sync to external locations. Includes history tracking, hover highlights, and context menu support.",
                    "Tools/GameDevTools/Asset Sync/Manager Window"
                );

                DrawToolItem(
                    "3.3 Hidden Dependency Detector", 
                    "Scan usage of shaders, textures, and more to find hidden dependencies in your project.",
                    "Tools/GameDevTools/Hidden Dependency Detector"
                );

                DrawToolItem(
                    "3.4 Integrated Terminal",
                    "Dockable terminal window with multi-tab support, command history, and shell selection (CMD, PowerShell).",
                    "Tools/GameDevTools/Integrated Terminal"
                );

                DrawToolItem(
                    "3.5 Note Dashboard",
                    "Centralized view of all Note components in the scene. Filter, search, and jump to notes instantly.",
                    "Tools/GameDevTools/Note Dashboard"
                );

                DrawToolItem(
                    "3.6 Code Editor",
                    "Standalone script editor with a searchable file browser, syntax highlighting, and Find/Replace support. Edit scripts without leaving Unity.",
                    "Tools/GameDevTools/Code Editor"
                );

                DrawToolItem(
                    "3.7 Quick Prefab Creator", 
                    "Create prefabs instantly from GameObjects. (Right-click > Prefab > Make Prefab)",
                    null // Passive tool / Context Menu
                );

                DrawToolItem(
                    "3.8 GSheet Data Viewer",
                    "View and edit Google Sheets data in Unity. Features dropdown support, real-time sync, and Service Account authentication.",
                    "Tools/GameDevTools/GSheet Data Viewer"
                );

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
            
            // Footer
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Version 1.0.0", EditorStyles.centeredGreyMiniLabel);
            //if (GUILayout.Button("Close", GUILayout.Height(30)))
            //{
            //    Close();
            //}
        }

        private void DrawToolItem(string title, string description, string menuPath)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(5);

            if (!string.IsNullOrEmpty(menuPath))
            {
                if (GUILayout.Button("Open Tool", GUILayout.Height(25)))
                {
                    EditorApplication.ExecuteMenuItem(menuPath);
                }
            }
            else
            {
                //// For passive or context tools, just show a label or instructions
                //EditorGUI.BeginDisabledGroup(true);
                //GUILayout.Button("Available via Context Menu / Automatic", GUILayout.Height(25));
                //EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
