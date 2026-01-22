using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDevTools.Editor
{
    [System.Serializable]
    public class TodoScannerUI
    {
        private enum TodoType { TODO, FIXME }

        private struct TodoItem
        {
            public string FilePath;
            public int LineNumber;
            public string Content;
            public TodoType Type;
        }

        private List<TodoItem> _items = new List<TodoItem>();
        private Vector2 _scrollPos;

        public void Initialize()
        {
            ScanProject();
        }

        public void Draw()
        {
            DrawToolbar();
            DrawList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                ScanProject();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox("No TODOs or FIXMEs found in C# scripts!", MessageType.Info);
            }
            else
            {
                foreach (var item in _items)
                {
                    DrawTodoItem(item);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTodoItem(TodoItem item)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Icon
            GUIContent icon = item.Type == TodoType.FIXME 
                ? EditorGUIUtility.IconContent("console.erroricon") 
                : EditorGUIUtility.IconContent("console.infoicon");
            
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

            EditorGUILayout.BeginVertical();

            // Clickable Content
            if (GUILayout.Button($"[{Path.GetFileName(item.FilePath)}:{item.LineNumber}] {item.Content}", EditorStyles.label))
            {
                // Open File
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(GetRelativePath(item.FilePath));
                AssetDatabase.OpenAsset(asset, item.LineNumber);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void ScanProject()
        {
            _items.Clear();
            string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                ScanFile(file);
            }
        }

        private void ScanFile(string path)
        {
            try 
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    // Simple parsing: Look for // TODO or // FIXME
                    if (line.StartsWith("//"))
                    {
                        if (line.ToUpper().Contains("TODO"))
                        {
                            string content = ExtractContent(line, "TODO");
                            _items.Add(new TodoItem
                            {
                                FilePath = path,
                                LineNumber = i + 1, // 1-based index for IDE
                                Content = content,
                                Type = TodoType.TODO
                            });
                        }
                        else if (line.ToUpper().Contains("FIXME"))
                        {
                            string content = ExtractContent(line, "FIXME");
                            _items.Add(new TodoItem
                            {
                                FilePath = path,
                                LineNumber = i + 1,
                                Content = content,
                                Type = TodoType.FIXME
                            });
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not scan file {path}: {ex.Message}");
            }
        }

        private string ExtractContent(string line, string tag)
        {
            int index = line.ToUpper().IndexOf(tag);
            string content = line.Substring(index + tag.Length).Trim();
            if (content.StartsWith(":")) content = content.Substring(1).Trim();
            return content;
        }

        private string GetRelativePath(string fullPath)
        {
            // Convert absolute path to relative Asset path
            if (fullPath.StartsWith(Application.dataPath))
            {
                return "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            }
            return fullPath;
        }
    }
}
