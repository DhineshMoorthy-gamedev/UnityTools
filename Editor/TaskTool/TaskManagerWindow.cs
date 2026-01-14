using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace UnityProductivityTools.TaskTool.Editor
{
    public class TaskManagerWindow : EditorWindow
    {
        private TaskData _taskData;
        private Vector2 _scrollPosition;
        private const string AssetPath = "Assets/Editor/Resources/TaskData.asset";

        [MenuItem("Tools/GameDevTools/Task Manager")]
        public static void ShowWindow()
        {
            TaskManagerWindow window = GetWindow<TaskManagerWindow>("Task Manager");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            LoadTaskData();
        }

        private void LoadTaskData()
        {
            _taskData = Resources.Load<TaskData>("TaskData");
            
            if (_taskData == null)
            {
                // Try to find it via asset database if Resources load fails (e.g. if it's not in a Resources folder yet)
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
            _taskData = CreateInstance<TaskData>();
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(_taskData, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Created new TaskData at {AssetPath}");
        }

        private void OnGUI()
        {
            if (_taskData == null)
            {
                LoadTaskData();
                if (_taskData == null)
                {
                    EditorGUILayout.HelpBox("Could not load or create Task Data.", MessageType.Error);
                    if (GUILayout.Button("Retry Load")) LoadTaskData();
                    return;
                }
            }

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Project Tasks", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Task", EditorStyles.toolbarButton))
            {
                _taskData.Tasks.Insert(0, new TaskItem()); // Add to top
                EditorUtility.SetDirty(_taskData);
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

            // Header Row
            EditorGUILayout.BeginHorizontal();
            
            // Toggle Expand
            task.IsExpanded = EditorGUILayout.Foldout(task.IsExpanded, string.IsNullOrEmpty(task.Title) ? "New Task" : task.Title, true, EditorStyles.boldLabel);
            
            // Status Badge
            TaskStatus newStatus = (TaskStatus)EditorGUILayout.EnumPopup(task.Status, GUILayout.Width(100));
            if (newStatus != task.Status)
            {
                task.Status = newStatus;
                EditorUtility.SetDirty(_taskData);
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                if (EditorUtility.DisplayDialog("Delete Task", "Are you sure you want to delete this task?", "Yes", "No"))
                {
                    _taskData.Tasks.RemoveAt(index);
                    EditorUtility.SetDirty(_taskData);
                    GUIUtility.ExitGUI(); // Stop drawing this frame
                }
            }
            EditorGUILayout.EndHorizontal();

            // Expanded Content
            if (task.IsExpanded)
            {
                EditorGUI.BeginChangeCheck();
                
                EditorGUILayout.Space(5);
                task.Title = EditorGUILayout.TextField("Title", task.Title);
                
                EditorGUILayout.BeginHorizontal();
                task.Priority = (TaskPriority)EditorGUILayout.EnumPopup("Priority", task.Priority);
                task.Owner = EditorGUILayout.TextField("Owner", task.Owner);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Description");
                task.Description = EditorGUILayout.TextArea(task.Description, GUILayout.Height(60));
                
                EditorGUILayout.LabelField($"Created: {task.CreatedDate}", EditorStyles.miniLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_taskData);
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
    }
}
