using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Dashboard
{
    public class DashboardWelcomeUI
    {
        private Vector2 scrollPos;

        public void Draw()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 20, 20) });

            GUILayout.Label("Welcome to Game Dev Tools", DashboardStyle.HeaderLabel);
            EditorGUILayout.LabelField("Your integrated suite for high-speed Unity development.", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(20);
            DashboardStyle.DrawLine();
            EditorGUILayout.Space(10);

            DrawSection("1. Workflow & Productivity", new[] {
                ("Feature Aggregator", "Organize scripts and assets by feature."),
                ("Project Bootstrapper", "Initialize new projects with standard structure."),
                ("Task Manager", "Local and Synced task tracking."),
                ("TODO Scanner", "Find and jump to //TODO comments.")
            });

            DrawSection("2. Scene & Hierarchy", new[] {
                ("Object Comparison", "Compare and sync GameObject states."),
                ("Object Grouper", "Non-destructive hierarchy grouping."),
                ("Snapshot Manager", "Capture and restore object states.")
            });

            DrawSection("3. Asset Utilities", new[] {
                ("Advanced Inspector", "Enhanced inspector with favorites and script editing."),
                ("Asset Sync", "Synchronize assets to external locations."),
                ("Hidden Dep Detector", "Find usage of shaders, textures, and more."),
                ("Integrated Terminal", "Multi-tab terminal within Unity.")
            });

            EditorGUILayout.Space(30);
            EditorGUILayout.HelpBox("Select a tool from the sidebar to get started.", MessageType.Info);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string title, (string name, string desc)[] items)
        {
            GUILayout.Label(title, DashboardStyle.SectionHeader);
            
            foreach (var item in items)
            {
                EditorGUILayout.BeginVertical(DashboardStyle.CardStyle);
                GUILayout.Label(item.name, EditorStyles.boldLabel);
                GUILayout.Label(item.desc, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
        }
    }
}
