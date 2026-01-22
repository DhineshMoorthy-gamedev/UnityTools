using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using UnityProductivityTools.TaskTool;

namespace UnityProductivityTools.TaskTool.Editor
{
    public class TaskManagerSyncedWindow : EditorWindow
    {
        private TaskManagerSyncedUI _ui;
        public static Action OnDataChanged;

        [MenuItem("Tools/GameDevTools/Task Manager (Synced)", false, 100)]
        public static void ShowWindow()
        {
            TaskManagerSyncedWindow window = GetWindow<TaskManagerSyncedWindow>("Task Manager (Synced)");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            Debug.Log("🛠 Task Manager Window Enabled - Checking Connection...");
            if (_ui == null) _ui = new TaskManagerSyncedUI();
            _ui.Initialize();
            
            OnDataChanged += BroadcastTasks;
            WebSocketEditorListener.OnConnected += BroadcastTasks;
        }

        private void OnDisable()
        {
            OnDataChanged -= BroadcastTasks;
            WebSocketEditorListener.OnConnected -= BroadcastTasks;
        }

        private void BroadcastTasks()
        {
            // We need access to the data to broadcast. 
            // In a real refactor we'd probably move Data management to a Service.
            // For now, we'll let the window handle the broadcast logic.
            
            // To avoid duplicating LoadTaskData, we might want to expose _taskData or just the JSON from UI.
            // Let's keep it simple: UI handles drawing, Window handles the "Live" aspects.
            
            // Re-loading just to get the reference if UI didn't expose it
            var data = Resources.Load<TaskData>("TaskData");
            if (data == null) return;
            
            string json = JsonUtility.ToJson(data);
            WSMessage msg = new WSMessage
            {
                sender = "editor",
                type = "task_sync",
                payload = json
            };

            WebSocketEditorListener.Send(msg);
            Debug.Log("📡 Broadcasted TaskData via WebSocket");
        }

        private void OnGUI()
        {
            if (_ui == null)
            {
                _ui = new TaskManagerSyncedUI();
                _ui.Initialize();
            }
            _ui.Draw();
        }

        public void UpdateData(string json)
        {
            _ui?.UpdateData(json);
            Repaint();
        }
    }
}
