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
        private bool _showTeamSettings = false;

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
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
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
                
                GUILayout.Space(10);
                GUI.enabled = false;
                GUILayout.Label("|", toolbarLabel, GUILayout.Width(10));
                GUI.enabled = true;

                // --- Online Presence Summary ---
                var activeClientsCopy = new List<ClientInfo>(_taskData.ActiveClients);
                if (activeClientsCopy.Count > 0)
                {
                    string presenceText = $"<b>Online ({activeClientsCopy.Count}):</b> ";
                    foreach (var client in activeClientsCopy)
                    {
                        presenceText += GetPlatformIcon(client.Platform) + " ";
                    }
                    
                    GUIStyle presenceStyle = new GUIStyle(toolbarLabel) { richText = true };
                    GUILayout.Label(presenceText, presenceStyle);
                }
                else
                {
                    GUILayout.Label("Online (0)", toolbarLabel);
                }
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Reconnect", EditorStyles.toolbarButton))
                {
                    WebSocketEditorListener.Reconnect();
                }

                if (GUILayout.Button("Disconnect", EditorStyles.toolbarButton))
                {
                    WebSocketEditorListener.Disconnect();
                }

                if (GUILayout.Button("Manage Team", EditorStyles.toolbarButton))
                {
                    _showTeamSettings = !_showTeamSettings;
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
            }

            if (_showTeamSettings)
            {
                DrawTeamSettings();
                EditorGUILayout.Space();
                GuiLine();
                EditorGUILayout.Space();
            }

            int taskToDeleteIndex = -1;
            
            EditorGUILayout.Space();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                
                var tasksCopy = new List<TaskItem>(_taskData.Tasks);
                var activeClientsCopy = new List<ClientInfo>(_taskData.ActiveClients);
                
                for (int i = 0; i < tasksCopy.Count; i++)
                {
                    if (DrawTaskItem(tasksCopy[i], i, activeClientsCopy))
                    {
                        taskToDeleteIndex = i;
                    }
                }

                if (tasksCopy.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tasks found. Click 'Add Task' to create one.", MessageType.Info);
                }
            }

            if (taskToDeleteIndex != -1)
            {
                _taskData.Tasks.RemoveAt(taskToDeleteIndex);
                EditorUtility.SetDirty(_taskData);
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }
        }

        private bool DrawTaskItem(TaskItem task, int index, List<ClientInfo> activeClients)
        {
            bool deleteRequested = false;
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            cardStyle.padding = new RectOffset(10, 10, 10, 10);
            cardStyle.margin = new RectOffset(5, 5, 5, 5);

            Color priorityColor = GetPriorityColor(task.Priority);
            GUI.backgroundColor = priorityColor;
            
            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                GUI.backgroundColor = Color.white;

                using (new EditorGUILayout.HorizontalScope())
                {
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
                            deleteRequested = true;
                        }
                    }
                }

                if (task.IsExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    
                    EditorGUILayout.Space(5);
                    task.Title = EditorGUILayout.TextField("Title", task.Title);
                    task.Priority = (TaskPriority)EditorGUILayout.EnumPopup("Priority", task.Priority);
                    
                    // --- Member Dropdowns ---
                    // Show anyone currently connected (ActiveClients)
                    List<string> onlineOptions = new List<string>();
                    foreach (var client in activeClients)
                    {
                        if (!onlineOptions.Contains(client.Name)) onlineOptions.Add(client.Name);
                    }

                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                // --- Assigner ---
                List<string> assignerOptions = new List<string> { "None" };
                assignerOptions.AddRange(onlineOptions);
                
                string currentAssigner = task.Assigner;
                if (currentAssigner == "Host Editor") currentAssigner = "";
                
                // If the assigned person is offline, show them in the list with an (Offline) tag
                string[] assignerDisplayOptions = new string[assignerOptions.Count];
                for (int i = 0; i < assignerOptions.Count; i++) assignerDisplayOptions[i] = assignerOptions[i];

                int assignerIndex = assignerOptions.IndexOf(currentAssigner);
                if (!string.IsNullOrEmpty(currentAssigner) && assignerIndex == -1)
                {
                    // Add the offline person to the display list
                    List<string> tempDisplay = new List<string>(assignerDisplayOptions);
                    tempDisplay.Add(currentAssigner + " (Offline)");
                    assignerDisplayOptions = tempDisplay.ToArray();
                    assignerIndex = assignerDisplayOptions.Length - 1;
                }
                else if (assignerIndex == -1) assignerIndex = 0;

                int newAssignerIndex = EditorGUILayout.Popup("Assigner", assignerIndex, assignerDisplayOptions);
                if (newAssignerIndex >= 0 && newAssignerIndex < assignerDisplayOptions.Length) 
                {
                    string selected = assignerDisplayOptions[newAssignerIndex];
                    if (selected == "None") task.Assigner = "";
                    else if (selected.EndsWith(" (Offline)")) { /* Keep it as is */ }
                    else task.Assigner = selected;
                }

                // --- Assignee ---
                List<string> assigneeOptions = new List<string> { "None" };
                assigneeOptions.AddRange(onlineOptions);

                string currentAssignee = task.Assignee;
                if (currentAssignee == "Host Editor") currentAssignee = "";

                string[] assigneeDisplayOptions = new string[assigneeOptions.Count];
                for (int i = 0; i < assigneeOptions.Count; i++) assigneeDisplayOptions[i] = assigneeOptions[i];

                int assigneeIndex = assigneeOptions.IndexOf(currentAssignee);
                if (!string.IsNullOrEmpty(currentAssignee) && assigneeIndex == -1)
                {
                    List<string> tempDisplay = new List<string>(assigneeDisplayOptions);
                    tempDisplay.Add(currentAssignee + " (Offline)");
                    assigneeDisplayOptions = tempDisplay.ToArray();
                    assigneeIndex = assigneeDisplayOptions.Length - 1;
                }
                else if (assigneeIndex == -1) assigneeIndex = 0;

                int newAssigneeIndex = EditorGUILayout.Popup("Assignee", assigneeIndex, assigneeDisplayOptions);
                if (newAssigneeIndex >= 0 && newAssigneeIndex < assigneeDisplayOptions.Length) 
                {
                    string selected = assigneeDisplayOptions[newAssigneeIndex];
                    if (selected == "None") task.Assignee = "";
                    else if (selected.EndsWith(" (Offline)")) { /* Keep it as is */ }
                    else task.Assignee = selected;
                }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(_taskData);
                        TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                    }

                    EditorGUILayout.LabelField("Description");
                    task.Description = EditorGUILayout.TextArea(task.Description, GUILayout.Height(60));

                    EditorGUILayout.Space(5);

                    // --- Linked Objects ---
                    EditorGUILayout.LabelField("Linked Objects", EditorStyles.boldLabel);
                    
                    for (int i = 0; i < task.Links.Count; i++)
                    {
                        TaskLink link = task.Links[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
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
                        }
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
            }
            return deleteRequested;
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

        private void DrawTeamSettings()
        {
            string projectId = string.IsNullOrEmpty(WebSocketEditorListener.CurrentProjectName) ? Application.productName : WebSocketEditorListener.CurrentProjectName;
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Invite System", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Invite Code: {projectId}");
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        EditorGUIUtility.systemCopyBuffer = projectId;
                        Debug.Log("📋 Invite Code copied to clipboard!");
                    }
                }
            }

            EditorGUILayout.Space(10);

            // --- Active Clients List ---
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var clientsToDraw = new List<ClientInfo>(_taskData.ActiveClients);
                int clientCount = clientsToDraw.Count;
                EditorGUILayout.LabelField($"Active Connections ({clientCount})", EditorStyles.boldLabel);
                
                if (clientCount > 0)
                {
                    foreach (var client in clientsToDraw)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string platformIcon = GetPlatformIcon(client.Platform);
                            EditorGUILayout.LabelField($"{platformIcon} {client.Name}", GUILayout.Width(150));
                            EditorGUILayout.LabelField($"[{client.Platform}]", EditorStyles.miniLabel, GUILayout.Width(80));
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField($"Seen {client.LastSeen}", EditorStyles.miniLabel);
                        }
                    }
                    
                    if (GUILayout.Button("Clear Inactive", EditorStyles.miniButton))
                    {
                        _taskData.ActiveClients.Clear(); 
                        EditorUtility.SetDirty(_taskData);
                        TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No other clients connected.", EditorStyles.miniLabel);
                }
            }
        }

        private void GuiLine(int i_height = 1)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        private string GetPlatformIcon(string platform)
        {
            string p = platform.ToLower();
            if (p.Contains("android") || p.Contains("iphone") || p.Contains("ios")) return "📱";
            if (p.Contains("editor")) return "💻";
            if (p.Contains("win") || p.Contains("mac")) return "🖥️";
            if (p.Contains("web") || p.Contains("html") || p.Contains("browser") || p.Contains("play")) return "🌐";
            
            // Fallback for runtime clients that don't send platform
            return "🌐"; 
        }
    }
}
