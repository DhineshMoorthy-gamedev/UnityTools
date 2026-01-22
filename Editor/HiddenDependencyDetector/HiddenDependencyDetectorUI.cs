using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenDeps
{
    [System.Serializable]
    public class HiddenDependencyDetectorUI
    {
        private enum Tab
        {
            ProjectScan,
            FileScan,
            FolderScan
        }

        private Tab currentTab;
        private MonoScript selectedScript;
        private DefaultAsset selectedFolder;
        private Vector2 scroll;

        private List<HiddenDependencyRecord> projectResults;
        private List<HiddenDependencyRecord> fileResults;
        private List<HiddenDependencyRecord> folderResults;
        private Dictionary<DependencyType, bool> dependencyTypeToggles;

        public void Initialize()
        {
            dependencyTypeToggles = new Dictionary<DependencyType, bool>();
            foreach (DependencyType type in System.Enum.GetValues(typeof(DependencyType)))
            {
                dependencyTypeToggles[type] = true;
            }
        }

        public void Draw()
        {
            DrawTabs();
            GUILayout.Space(5);

            DrawDependencyTypeInfo();
            GUILayout.Space(10);

            switch (currentTab)
            {
                case Tab.ProjectScan:
                    DrawProjectScanTab();
                    DrawResults(projectResults);
                    break;
                case Tab.FileScan:
                    DrawFileScanTab();
                    DrawResults(fileResults);
                    break;
                case Tab.FolderScan:
                    DrawFolderScanTab();
                    DrawResults(folderResults);
                    break;
            }
        }

        private void DrawDependencyTypeInfo()
        {
            string[] dependencyTypes = System.Enum.GetNames(typeof(DependencyType));
            string typesText = string.Join(", ", dependencyTypes);

            EditorGUILayout.HelpBox(
                $"Scanning only for the following dependency types:\n{typesText}",
                MessageType.Info
            );
            GUILayout.Space(5);
        }

        private void DrawTabs()
        {
            currentTab = (Tab)GUILayout.Toolbar(
                (int)currentTab,
                new[] { "Project Scan", "File Scan", "Folder Scan" }
            );
        }

        private void DrawProjectScanTab()
        {
            if (GUILayout.Button("Scan Entire Project", GUILayout.Height(30)))
            {
                string[] allScripts = AssetDatabase.FindAssets("t:MonoScript");
                int scriptCount = allScripts.Length;

                bool proceed = EditorUtility.DisplayDialog(
                    "Project Scan Warning",
                    $"You are about to scan the entire project for hidden dependencies.\n\n" +
                    $"This may take some time depending on the number of scripts in your project.\n" +
                    $"Total scripts detected: {scriptCount}\n\n" +
                    "Do you want to proceed?",
                    "Scan",
                    "Cancel"
                );

                if (!proceed) return;

                projectResults = HiddenDependencyScanner.ScanProject();
            }
        }

        private void DrawFileScanTab()
        {
            EditorGUILayout.LabelField("Select C# Script to Scan", EditorStyles.boldLabel);

            selectedScript = (MonoScript)EditorGUILayout.ObjectField(
                selectedScript,
                typeof(MonoScript),
                false
            );

            GUILayout.Space(5);

            if (GUILayout.Button("Scan Selected Script", GUILayout.Height(25)))
            {
                if (selectedScript == null)
                {
                    EditorUtility.DisplayDialog("No Script Selected", "Please select a C# script to scan.", "OK");
                    return;
                }

                string path = AssetDatabase.GetAssetPath(selectedScript);
                fileResults = HiddenDependencyScanner.ScanSingleScript(path);
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Scan Active Selection"))
            {
                if (Selection.activeObject is MonoScript script)
                {
                    selectedScript = script;
                    string path = AssetDatabase.GetAssetPath(script);
                    fileResults = HiddenDependencyScanner.ScanSingleScript(path);
                }
                else
                {
                    EditorUtility.DisplayDialog("No MonoScript Selected", "Please select a C# script in the Project window.", "OK");
                }
            }
        }

        private void DrawFolderScanTab()
        {
            EditorGUILayout.LabelField("Select Folder to Scan", EditorStyles.boldLabel);

            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                selectedFolder,
                typeof(DefaultAsset),
                false
            );

            GUILayout.Space(5);

            if (GUILayout.Button("Scan Selected Folder", GUILayout.Height(25)))
            {
                if (selectedFolder == null)
                {
                    EditorUtility.DisplayDialog("No Folder Selected", "Please select a folder in the Project window.", "OK");
                    return;
                }

                string folderPath = AssetDatabase.GetAssetPath(selectedFolder);
                folderResults = ScanFolder(folderPath);
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Scan Active Selection Folder"))
            {
                if (Selection.activeObject is DefaultAsset folder)
                {
                    selectedFolder = folder;
                    string folderPath = AssetDatabase.GetAssetPath(folder);
                    folderResults = ScanFolder(folderPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("No Folder Selected", "Please select a folder in the Project window.", "OK");
                }
            }
        }

        private List<HiddenDependencyRecord> ScanFolder(string folderPath)
        {
            List<HiddenDependencyRecord> results = new List<HiddenDependencyRecord>();

            if (!Directory.Exists(folderPath)) return results;

            string[] files = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                results.AddRange(HiddenDependencyScanner.ScanSingleScript(file));
            }

            return results;
        }

        private void DrawResults(List<HiddenDependencyRecord> results)
        {
            if (results == null) return;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Found {results.Count} hidden dependencies", EditorStyles.boldLabel);

            if (GUILayout.Button("Clear Results", GUILayout.Width(120), GUILayout.Height(20)))
            {
                results.Clear();
                scroll = Vector2.zero;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox("No hidden dependencies found.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var r in results)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField("Script", r.scriptName);
                EditorGUILayout.LabelField("Line", r.lineNumber.ToString());
                EditorGUILayout.LabelField("Dependency Type", r.dependencyType.ToString());
                EditorGUILayout.LabelField("Target", r.target);
                EditorGUILayout.LabelField("Code", r.rawLine);

                if (GUILayout.Button("Ping Script"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(r.scriptPath);
                    EditorGUIUtility.PingObject(script);
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
