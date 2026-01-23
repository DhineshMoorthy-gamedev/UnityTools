using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using UnityProductivityTools.TaskTool;

namespace UnityProductivityTools.TaskTool.Editor
{
    [System.Serializable]
    public class TaskManagerUI
    {
        private TaskData _taskData;
        private Vector2 _scrollPosition;
        private const string AssetPath = "Assets/Editor/Resources/TaskData.asset";

        private TeamData _teamData;
        private const string TeamAssetPath = "Assets/Editor/Resources/TeamData.asset";
        private bool _showTeamSettings = false;

        public void Initialize()
        {
            LoadTaskData();
            LoadTeamData();
        }

        public void Draw()
        {
            if (_taskData == null) LoadTaskData();
            if (_teamData == null) LoadTeamData();

            if (_taskData == null || _teamData == null) return;

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Project Tasks", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Manage Team", EditorStyles.toolbarButton))
            {
                _showTeamSettings = !_showTeamSettings;
            }
            if (GUILayout.Button("Add Task", EditorStyles.toolbarButton))
            {
                _taskData.Tasks.Insert(0, new TaskItem()); // Add to top
                EditorUtility.SetDirty(_taskData);
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                LoadTaskData();
                LoadTeamData();
            }
            EditorGUILayout.EndHorizontal();

            if (_showTeamSettings)
            {
                DrawTeamSettings();
                EditorGUILayout.Space();
                GuiLine();
                EditorGUILayout.Space();
            }

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

        private void LoadTaskData()
        {
            _taskData = Resources.Load<TaskData>("TaskData");
            
            if (_taskData == null)
            {
                // Try to find it via asset database
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
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(_taskData, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void LoadTeamData()
        {
            _teamData = Resources.Load<TeamData>("TeamData");
            if (_teamData == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:TeamData");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _teamData = AssetDatabase.LoadAssetAtPath<TeamData>(path);
                }
            }

            if (_teamData == null)
            {
                CreateTeamData();
            }
        }

        private void CreateTeamData()
        {
            _teamData = ScriptableObject.CreateInstance<TeamData>();
            
            // Ensure directory exists
             string directory = Path.GetDirectoryName(TeamAssetPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            AssetDatabase.CreateAsset(_teamData, TeamAssetPath);
            AssetDatabase.SaveAssets();
            
            // Auto-migrate existing users
            if (_taskData != null)
            {
                foreach (var task in _taskData.Tasks)
                {
                    AddMemberIfNotExists(task.Assigner);
                    AddMemberIfNotExists(task.Assignee);
                }
            }
            EditorUtility.SetDirty(_teamData);
            AssetDatabase.SaveAssets();
        }

        private void AddMemberIfNotExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!_teamData.Members.Contains(name))
            {
                _teamData.Members.Add(name);
            }
        }

        private void DrawTeamSettings()
        {
             EditorGUILayout.HelpBox("Manage Team Members (populates dropdowns)", MessageType.Info);
             
             SerializedObject so = new SerializedObject(_teamData);
             SerializedProperty membersProp = so.FindProperty("Members");
             
             EditorGUILayout.PropertyField(membersProp, true); // Use default list UI
             
             if (so.ApplyModifiedProperties())
             {
                 EditorUtility.SetDirty(_teamData);
             }
        }

        private void GuiLine( int i_height = 1 )
        {
           Rect rect = EditorGUILayout.GetControlRect(false, i_height );
           rect.height = i_height;
           EditorGUI.DrawRect(rect, new Color ( 0.5f,0.5f,0.5f, 1 ) );
        }

        private void DrawTaskItem(TaskItem task, int index)
        {
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            cardStyle.padding = new RectOffset(10, 10, 10, 10);
            cardStyle.margin = new RectOffset(5, 5, 5, 5);

            Color priorityColor = GetPriorityColor(task.Priority);
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = priorityColor;
            
            EditorGUILayout.BeginVertical(cardStyle);
            GUI.backgroundColor = originalColor;

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
                EditorGUILayout.EndHorizontal();

                // TEAM MEMBER DROPDOWNS
                string[] options = _teamData.Members.ToArray();
                
                EditorGUILayout.BeginHorizontal();
                
                // Assigner
                int assignerIndex = -1;
                if (!string.IsNullOrEmpty(task.Assigner)) assignerIndex = System.Array.IndexOf(options, task.Assigner);
                
                int newAssignerIndex = EditorGUILayout.Popup("Assigner", assignerIndex, options);
                if (newAssignerIndex >= 0 && newAssignerIndex < options.Length) task.Assigner = options[newAssignerIndex];
                
                // Assignee
                int assigneeIndex = -1;
                if (!string.IsNullOrEmpty(task.Assignee)) assigneeIndex = System.Array.IndexOf(options, task.Assignee);
                
                int newAssigneeIndex = EditorGUILayout.Popup("Assignee", assigneeIndex, options);
                if (newAssigneeIndex >= 0 && newAssigneeIndex < options.Length) task.Assignee = options[newAssigneeIndex];

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Description");
                task.Description = EditorGUILayout.TextArea(task.Description, GUILayout.Height(60));

                EditorGUILayout.Space(5);

                // --- Deep Linking Logic ---
                EditorGUILayout.LabelField("Linked Objects", EditorStyles.boldLabel);
                

                // 2. Draw List
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
                         if (newObject == null) // Clear content
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
                             link.GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(newObject).ToString();
                             link.ObjectName = newObject.name;
                             link.ObjectType = newObject.GetType().Name;

                             if (newObject is GameObject go) 
                                 link.ScenePath = go.scene.path;
                             else if (newObject is Component comp)
                                 link.ScenePath = comp.gameObject.scene.path;
                             else
                                 link.ScenePath = null;
                         }
                         EditorUtility.SetDirty(_taskData);
                    }

                    // --- Cross-Scene Support UI ---
                    if (currentObject == null && !string.IsNullOrEmpty(link.GlobalObjectId))
                    {
                        Color originalGuiColor = GUI.color;
                        GUI.color = new Color(0.7f, 0.7f, 1f);
                        
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
                        i--; // Adjust index
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

                
                EditorGUILayout.LabelField($"Created: {task.CreatedDate}", EditorStyles.miniLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_taskData);
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
