using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools
{
    public class GameDevToolsWelcomeWindow : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/GameDevTools/Welcome")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameDevToolsWelcomeWindow>("Tools Welcome");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space();
            GUILayout.Label("Game Dev Tools", EditorStyles.boldLabel);
            GUILayout.Label("Productivity & Workflow Suite", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Description
            EditorGUILayout.HelpBox("Welcome! This package contains tools to speed up your Unity workflow. Explore the available tools below.", MessageType.Info);
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // 1. Snapshot Manager
            DrawToolItem(
                "Snapshot Manager", 
                "Capture, View, and Restore GameObject states (Transforms & Component values). Includes undo support.",
                "Tools/GameDevTools/Snapshot Manager"
            );

            // 2. Hidden Dependency Detector
            DrawToolItem(
                "Hidden Dependency Detector", 
                "Scan usage of shaders, textures, and more to find hidden dependencies in your project.",
                "Tools/GameDevTools/Hidden Dependency Detector"
            );

            // 3. Quick Prefab Creator
            DrawToolItem(
                "Quick Prefab Creator", 
                "Create prefabs instantly from GameObjects. (Right-click > Prefab > Make Prefab)",
                null // Passive tool / Context Menu
            );

            // 4. Add Scene to Build
            DrawToolItem(
                "Add Scene to Build", 
                "Quickly add the selected scene asset to the Build Settings. (Right-click on Scene Asset in Project window > Add to Build Settings)",
                null // Context Menu
            );
            
            // 5. Hierarchy Icons
            DrawToolItem(
                "Hierarchy Icons", 
                "Auto-displays icons in the Hierarchy view for GameObjects with specific components.",
                null // Passive
            );

            // 6. Task Manager
            DrawToolItem(
                "Task Manager",
                "Project-wide task tracking tool. Assign priorities, owners, and statuses to keep track of your work.",
                "Tools/GameDevTools/Task Manager"
            );

            // 7. Object Grouper
            DrawToolItem(
                "Object Grouper",
                "Organize objects into logical groups without modifying the Hierarchy. Supports bulk visibility, locking, and selection.",
                "Tools/GameDevTools/Object Grouper"
            );

            // 8. Project Bootstrapper
            DrawToolItem(
                "Project Bootstrapper",
                "Initialize new projects with standard folders, base scripts (Singleton, ObjectPool), and scenes in seconds.",
                "Tools/GameDevTools/Project Bootstrapper"
            );

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
