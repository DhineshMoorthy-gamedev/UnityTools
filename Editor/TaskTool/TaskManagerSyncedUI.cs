using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            
            // Shared style for alignment
            GUIStyle toolbarLabel = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft, margin = new RectOffset(0, 5, 2, 0) };

            GUI.color = isConnected ? Color.green : Color.red;
            GUILayout.Label(" ● ", EditorStyles.boldLabel, GUILayout.Width(15));
            GUI.color = Color.white;
            
            GUILayout.Label(isConnected ? "Connected" : "Offline", toolbarLabel, GUILayout.Width(60));
            
            GUILayout.Space(5);
            GUI.enabled = false;
            GUILayout.Label("|", toolbarLabel, GUILayout.Width(10));
            GUI.enabled = true;
            
            GUI.color = new Color(0.6f, 0.8f, 1f);
            string displayId = string.IsNullOrEmpty(WebSocketEditorListener.CurrentProjectName) ? Application.productName : WebSocketEditorListener.CurrentProjectName;
            GUILayout.Label($"Project ID: {displayId}", toolbarLabel);
            GUI.color = Color.white;
            
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

                // --- Linked Objects ---
                EditorGUILayout.LabelField("Linked Objects", EditorStyles.boldLabel);
                
                for (int i = 0; i < task.Links.Count; i++)
                {
                    TaskLink link = task.Links[i];
                    EditorGUILayout.BeginHorizontal();
                    
                    UnityEngine.Object currentObject = link.Object;

                    // Try to resolve reference from GlobalObjectId if Object is missing
                    if (currentObject == null && !string.IsNullOrEmpty(link.GlobalObjectId))
                    {
                        if (GlobalObjectId.TryParse(link.GlobalObjectId, out GlobalObjectId id))
                        {
                            currentObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                            
                            // AUTO-CAPTURE: If we found it, ensure ScenePath and metadata are up to date
                            if (currentObject != null)
                            {
                                bool changed = false;
                                if (link.ObjectName != currentObject.name) { link.ObjectName = currentObject.name; changed = true; }
                                if (link.ObjectType != currentObject.GetType().Name) { link.ObjectType = currentObject.GetType().Name; changed = true; }
                                
                                string detectedScene = null;
                                if (currentObject is GameObject go && go.scene.IsValid()) detectedScene = go.scene.path;
                                else if (currentObject is Component comp && comp.gameObject.scene.IsValid()) detectedScene = comp.gameObject.scene.path;
                                
                                if (!string.IsNullOrEmpty(detectedScene) && link.ScenePath != detectedScene)
                                {
                                    link.ScenePath = detectedScene;
                                    changed = true;
                                }
                                
                                if (changed) EditorUtility.SetDirty(_taskData);
                            }
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    UnityEngine.Object newObject = EditorGUILayout.ObjectField(GUIContent.none, currentObject, typeof(UnityEngine.Object), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newObject == null)
                        {
                            link.Object = null;
                            link.GlobalObjectId = null;
                            link.ObjectName = null;
                            link.ObjectType = null;
                            link.ScenePath = null;
                        }
                        else
                        {
                            link.Object = newObject;
                            link.ObjectName = newObject.name;
                            link.ObjectType = newObject.GetType().Name;
                            link.GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(newObject).ToString();
                            
                            if (newObject is GameObject go) 
                                link.ScenePath = go.scene.path;
                            else if (newObject is Component comp)
                                link.ScenePath = comp.gameObject.scene.path;
                            else
                                link.ScenePath = null; // Assets don't have a scene path
                        }
                        EditorUtility.SetDirty(_taskData);
                    }

                    // --- Cross-Scene Support UI ---
                    if (currentObject == null && !string.IsNullOrEmpty(link.GlobalObjectId))
                    {
                        Color originalGuiColor = GUI.color;
                        GUI.color = new Color(0.7f, 0.7f, 1f); // Subtle blue to indicate it's a known but unloaded object
                        
                        string sceneInfo = !string.IsNullOrEmpty(link.ScenePath) ? $" @ {Path.GetFileNameWithoutExtension(link.ScenePath)}" : "";
                        GUILayout.Label($"[{link.ObjectName}] ({link.ObjectType}){sceneInfo}", EditorStyles.miniLabel);
                        
                        GUI.color = originalGuiColor;

                        if (!string.IsNullOrEmpty(link.ScenePath) && link.ScenePath != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
                        {
                            if (GUILayout.Button("Go to Scene", EditorStyles.miniButton, GUILayout.Width(80)))
                            {
                                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                {
                                    EditorSceneManager.OpenScene(link.ScenePath);
                                }
                            }
                        }
                    }

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        task.Links.RemoveAt(i);
                        EditorUtility.SetDirty(_taskData);
                        i--;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }

                // 3. Add Button + Drop Area
                Rect dropArea = GUILayoutUtility.GetRect(0.0f, 25.0f, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drag & Drop Objects here or Click to Add", EditorStyles.helpBox);
                
                if (Event.current.type == EventType.DragUpdated && dropArea.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                
                if (Event.current.type == EventType.DragPerform && dropArea.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.AcceptDrag();
                    foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                    {
                        AddLinkFromObject(task, obj);
                    }
                    Event.current.Use();
                }

                if (GUI.Button(dropArea, "", GUIStyle.none)) // Still allow clicking through the box
                {
                    task.Links.Add(new TaskLink());
                    EditorUtility.SetDirty(_taskData);
                }
                // --------------------------

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

        private void AddLinkFromObject(TaskItem task, UnityEngine.Object obj)
        {
            if (obj == null) return;
            
            TaskLink newLink = new TaskLink
            {
                Object = obj,
                ObjectName = obj.name,
                ObjectType = obj.GetType().Name,
                GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString()
            };

            if (obj is GameObject go) 
                newLink.ScenePath = go.scene.path;
            else if (obj is Component comp)
                newLink.ScenePath = comp.gameObject.scene.path;
            
            task.Links.Add(newLink);
            EditorUtility.SetDirty(_taskData);
            TaskManagerSyncedWindow.OnDataChanged?.Invoke();
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
