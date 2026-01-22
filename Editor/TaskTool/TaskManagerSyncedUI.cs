using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using UnityProductivityTools.TaskTool;

namespace UnityProductivityTools.TaskTool.Editor
{
    [Serializable]
    public class TaskManagerSyncedUI
    {
        private TaskData _taskData;
        private Vector2 _scrollPosition;
        private const string AssetPath = "Assets/Editor/Resources/TaskData.asset";

        public void Initialize()
        {
            LoadTaskData();
        }

        private void LoadTaskData()
        {
            _taskData = Resources.Load<TaskData>("TaskData");
            
            if (_taskData == null)
            {
                 string[] guids = AssetDatabase.FindAssets("t:TaskData");
                 if (guids.Length > 0)
                 {
                     string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                     _taskData = AssetDatabase.LoadAssetAtPath<TaskData>(path);
                 }
            }

            if (_taskData == null)
            {
                CreateTaskData();
            }
        }

        private void CreateTaskData()
        {
            _taskData = ScriptableObject.CreateInstance<TaskData>();
            string directory = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(_taskData, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void Draw()
        {
            if (_taskData == null) LoadTaskData();

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            bool isConnected = WebSocketEditorListener.IsConnected;
            GUI.color = isConnected ? Color.green : Color.red;
            GUILayout.Label(" ● ", EditorStyles.boldLabel, GUILayout.Width(20));
            GUI.color = Color.white;
            
            GUILayout.Label(isConnected ? "Connected" : $"Disconnected ({WebSocketEditorListener.SocketStatus})", EditorStyles.miniLabel);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Reconnect", EditorStyles.toolbarButton))
            {
                WebSocketEditorListener.Reconnect();
            }

            if (GUILayout.Button("Disconnect", EditorStyles.toolbarButton))
            {
                WebSocketEditorListener.Disconnect();
            }

            if (GUILayout.Button("Add Task", EditorStyles.toolbarButton))
            {
                _taskData.Tasks.Insert(0, new TaskItem());
                EditorUtility.SetDirty(_taskData);
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }
            
            if (GUILayout.Button("Sync All", EditorStyles.toolbarButton))
            {
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                LoadTaskData();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            for (int i = 0; i < _taskData.Tasks.Count; i++)
            {
                DrawTaskItem(_taskData.Tasks[i], i);
            }

            if (_taskData.Tasks.Count == 0)
            {
                EditorGUILayout.HelpBox("No tasks found. Click 'Add Task' to create one.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTaskItem(TaskItem task, int index)
        {
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            cardStyle.padding = new RectOffset(10, 10, 10, 10);
            cardStyle.margin = new RectOffset(5, 5, 5, 5);

            Color priorityColor = GetPriorityColor(task.Priority);
            GUI.backgroundColor = priorityColor;
            
            EditorGUILayout.BeginVertical(cardStyle);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginChangeCheck();
            task.IsExpanded = EditorGUILayout.Foldout(task.IsExpanded, string.IsNullOrEmpty(task.Title) ? "New Task" : task.Title, true, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                 EditorUtility.SetDirty(_taskData);
            }
            
            TaskStatus newStatus = (TaskStatus)EditorGUILayout.EnumPopup(task.Status, GUILayout.Width(100));
            if (newStatus != task.Status)
            {
                task.Status = newStatus;
                EditorUtility.SetDirty(_taskData);
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                if (EditorUtility.DisplayDialog("Delete Task", "Are you sure you want to delete this task?", "Yes", "No"))
                {
                    _taskData.Tasks.RemoveAt(index);
                    EditorUtility.SetDirty(_taskData);
                    TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (task.IsExpanded)
            {
                EditorGUI.BeginChangeCheck();
                
                EditorGUILayout.Space(5);
                task.Title = EditorGUILayout.TextField("Title", task.Title);
                task.Priority = (TaskPriority)EditorGUILayout.EnumPopup("Priority", task.Priority);
                task.Assignee = EditorGUILayout.TextField("Assignee", task.Assignee);
                task.Assigner = EditorGUILayout.TextField("Assigner", task.Assigner);

                EditorGUILayout.LabelField("Description");
                task.Description = EditorGUILayout.TextArea(task.Description, GUILayout.Height(60));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Created: {task.CreatedDate}", EditorStyles.miniLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_taskData);
                }
                
                if (GUILayout.Button("Save Changes & Sync"))
                {
                    TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetPriorityColor(TaskPriority priority)
        {
            switch (priority)
            {
                case TaskPriority.Low: return new Color(0.95f, 0.95f, 0.95f);
                case TaskPriority.Medium: return new Color(0.8f, 0.9f, 1f);
                case TaskPriority.High: return new Color(1f, 0.9f, 0.7f);
                case TaskPriority.Critical: return new Color(1f, 0.7f, 0.7f);
                default: return Color.white;
            }
        }

        public void UpdateData(string json)
        {
            if (_taskData != null)
            {
                JsonUtility.FromJsonOverwrite(json, _taskData);
                EditorUtility.SetDirty(_taskData);
            }
        }
    }
}
