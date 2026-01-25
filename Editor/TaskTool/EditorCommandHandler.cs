using UnityEngine;
using UnityEditor.Build.Reporting;

using System;
using System.IO;

using UnityProductivityTools.TaskTool;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityProductivityTools.TaskTool.Editor
{
    public static class EditorCommandHandler
    {
        public static void Handle(WSMessage msg)
        {
            if (msg.sender != "mobile" && msg.sender != "editor" && msg.sender != "server")
                return;

            // Filter by Project ID
            string currentId = string.IsNullOrEmpty(WebSocketEditorListener.CurrentProjectName) ? Application.productName : WebSocketEditorListener.CurrentProjectName;
            
            if (!string.IsNullOrEmpty(msg.projectId) && msg.projectId != currentId)
            {
                return;
            }

            switch (msg.type)
            {
                case "identity":
                    RegisterClient(msg);
                    break;
                
                case "command":
                    if (msg.payload == "Start Build") StartBuild();
                    else if (msg.payload == "Play Mode On") EnterPlayMode();
                    else if (msg.payload == "Play Mode Off") ExitPlayMode();
                    break;

                case "task_sync":
                    SyncTasks(msg.payload);
                    break;

                case "request_sync":
                    SendCurrentTaskList(msg.senderId);
                    break;
                
                case "ping_object":
                    PingObject(msg.payload, msg.scenePath);
                    break;
                
                case "clear_presence":
                    ClearPresence();
                    break;

                case "presence_sync":
                    HandlePresenceSync(msg.payload);
                    break;
                
                case "member_join":
                    RegisterClient(msg);
                    var win = EditorWindow.GetWindow<TaskManagerSyncedWindow>();
                    if (win != null) win.ShowNotification(new GUIContent($"Client Joined: {msg.payload}"));
                    break;

                case "member_leave":
                    RemoveActiveClient(msg.senderId);
                    break;

                default:
                    // Only register on heartbeat/log etc, NOT on sync messages
                    if (msg.type != "task_sync" && msg.type != "presence_sync")
                    {
                        RegisterClient(msg);
                    }
                    break;
            }
        }

        // ✅ THIS METHOD WAS MISSING OR MIS-SCOPED
        static void StartBuild()
        {
            Debug.Log("🚀 Mobile requested BUILD");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string location = EditorUserBuildSettings.GetBuildLocation(target);

            if (string.IsNullOrEmpty(location))
            {
                Debug.LogError("❌ Build location is empty. Set it once manually.");
                return;
            }

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetScenePaths(),
                locationPathName = location,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log($"🏗 Starting build → {target} → {location}");

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

            Debug.Log($"📦 Build result: {report.summary.result}");
            Debug.Log($"⏱ Time: {report.summary.totalTime}");
            Debug.Log($"📁 Size: {report.summary.totalSize / (1024 * 1024)} MB");

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("❌ BUILD FAILED");
            }
            else
            {
                Debug.Log("✅ BUILD SUCCEEDED");
            }
        }


        static string[] GetScenePaths()
        {
            string[] scenes = new string[EditorBuildSettings.scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = EditorBuildSettings.scenes[i].path;
            }
            return scenes;
        }

        static void EnterPlayMode()
        {
            Debug.Log("▶ Enter Play Mode");
            EditorApplication.isPlaying = true;
        }

        static void ExitPlayMode()
        {
            Debug.Log("⏹ Exit Play Mode");
            EditorApplication.isPlaying = false;
        }

        static void SendCurrentTaskList(string requesterId)
        {
            Debug.Log($"📤 client {requesterId} requested initial task sync");
            
            // Load the current TaskData
            var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
            
            if (taskData == null)
            {
                // Try to find it via AssetDatabase
                string[] guids = AssetDatabase.FindAssets("t:TaskData");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    taskData = AssetDatabase.LoadAssetAtPath<TaskData>(path);
                }
            }
            
            if (taskData != null)
            {
                string json = JsonUtility.ToJson(taskData);
                WSMessage msg = new WSMessage
                {
                    sender = "editor",
                    type = "task_sync",
                    payload = json,
                    projectId = string.IsNullOrEmpty(WebSocketEditorListener.CurrentProjectName) ? Application.productName : WebSocketEditorListener.CurrentProjectName,
                    targetId = requesterId // Set the target receiver
                };
                
                WebSocketEditorListener.Send(msg);
                int taskCount = taskData.Tasks != null ? taskData.Tasks.Count : 0;
                Debug.Log($"✅ Sent {taskCount} tasks to client {requesterId}");
            }
            else
            {
                Debug.LogWarning("⚠ No TaskData found to send");
            }
        }

        static void SyncTasks(string json)
        {
            Debug.Log("🔄 Syncing Tasks...");
            var window = EditorWindow.GetWindow<TaskManagerSyncedWindow>();
            if (window != null)
            {
                window.UpdateData(json);
            }
        }

        static void PingObject(string globalObjectId, string scenePath = null)
        {
            if (string.IsNullOrEmpty(globalObjectId)) return;

            Debug.Log($"🎯 Pinging Object: {globalObjectId} in scene: {scenePath}");

            // Handle cross-scene pinging
            if (!string.IsNullOrEmpty(scenePath) && scenePath != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
                else
                {
                    Debug.LogWarning("❌ Scene open cancelled by user. Cannot ping object.");
                    return;
                }
            }

            if (GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId id))
            {
                UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
                else
                {
                    Debug.LogWarning($"⚠ Could not resolve object for ID: {globalObjectId}");
                }
            }
            else
            {
                Debug.LogError($"❌ Invalid GlobalObjectId: {globalObjectId}");
            }
        }

        static void RegisterClient(WSMessage msg, bool shouldBroadcast = true)
        {
            if (string.IsNullOrEmpty(msg.senderId) || msg.sender == "editor" || msg.type == "task_sync") return;

            var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
            if (taskData == null) return;

            bool structuralChange = false;
            
            // Match by exact SenderId OR by the short ID suffix in the name (for migration/cleanup)
            var existing = taskData.ActiveClients.Find(c => c.SenderId == msg.senderId || 
                         (!string.IsNullOrEmpty(c.Name) && c.Name.Contains(msg.senderId.Substring(0, Mathf.Min(4, msg.senderId.Length)))));
            
            string deviceShortId = msg.senderId.Length > 4 ? msg.senderId.Substring(0, 4) : msg.senderId;
            string baseName = $"Client {deviceShortId}";
            
            if (msg.type == "member_join" && !string.IsNullOrEmpty(msg.payload)) baseName = msg.payload;
            else if (msg.type == "identity" && !string.IsNullOrEmpty(msg.payload)) baseName = msg.payload;

            string platform = !string.IsNullOrEmpty(msg.platform) ? msg.platform : "Web";
            if (platform.ToLower() == "unknown") platform = "Web";

            if (existing == null)
            {
                taskData.ActiveClients.RemoveAll(c => c.Name == baseName || (c.SenderId != null && c.SenderId.Contains(deviceShortId)));
                
                taskData.ActiveClients.Add(new ClientInfo
                {
                    SenderId = msg.senderId,
                    Name = baseName,
                    Platform = platform,
                    LastSeen = DateTime.Now.ToString("HH:mm:ss")
                });
                structuralChange = true;
            }
            else
            {
                if (existing.Name != baseName || existing.Platform != platform) structuralChange = true;
                
                existing.SenderId = msg.senderId;
                existing.Name = baseName;
                existing.Platform = platform;
                existing.LastSeen = DateTime.Now.ToString("HH:mm:ss");
            }

            if (structuralChange)
            {
                EditorUtility.SetDirty(taskData);
                if (shouldBroadcast) BroadcastClientList(taskData);
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }
        }

        public static void BroadcastClientList(TaskData taskData)
        {
            if (taskData == null) return;
            string json = JsonUtility.ToJson(taskData);
            WSMessage syncMsg = new WSMessage
            {
                sender = "editor",
                type = "task_sync",
                payload = json
            };
            WebSocketEditorListener.Send(syncMsg);
        }

        public static void ClearPresence()
        {
            var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
            if (taskData == null) return;

            Debug.Log("🧹 Clearing presence list (Editor is the host)...");
            taskData.ActiveClients.Clear();
            
            EditorUtility.SetDirty(taskData);
            AssetDatabase.SaveAssets();
            TaskManagerSyncedWindow.OnDataChanged?.Invoke();
        }

        private static void HandlePresenceSync(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                // The server sends a JSON array of member objects
                // Wrap it in a container for JsonUtility or use a simple hack
                // Actually, let's just parse it manually or via a wrapper
                string wrappedJson = "{\"items\":" + json + "}";
                PresenceSyncWrapper wrapper = JsonUtility.FromJson<PresenceSyncWrapper>(wrappedJson);

                if (wrapper != null && wrapper.items != null)
                {
                    var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
                    if (taskData == null) return;

                    foreach (var member in wrapper.items)
                    {
                        // Use RegisterClient for each member to handle logic consistently
                        WSMessage memberMsg = new WSMessage
                        {
                            type = "identity",
                            sender = member.sender,
                            senderId = member.senderId,
                            payload = member.name,
                            platform = member.platform
                        };
                        RegisterClient(memberMsg, false);
                    }
                    
                    Debug.Log($"👥 [WS] Successfully synced {wrapper.items.Length} existing members from server.");
                    
                    // One single broadcast and save after bulk registration
                    EditorUtility.SetDirty(taskData);
                    AssetDatabase.SaveAssets(); // Doing it once here is fine
                    BroadcastClientList(taskData);
                    TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("❌ Failed to parse presence sync: " + e.Message);
            }
        }

        [Serializable]
        private class PresenceSyncWrapper
        {
            public PresenceMember[] items;
        }

        [Serializable]
        private class PresenceMember
        {
            public string senderId;
            public string name;
            public string platform;
            public string sender;
        }

        static void RemoveActiveClient(string senderId)
        {
            var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
            if (taskData == null) return;

            int removed = taskData.ActiveClients.RemoveAll(c => c.SenderId == senderId);
            Debug.Log($"[DEBUG] RemoveActiveClient called for {senderId}. Removed count: {removed}");
            
            if (removed > 0)
            {
                EditorUtility.SetDirty(taskData);
                AssetDatabase.SaveAssets();
                BroadcastClientList(taskData);
                
                int listenerCount = TaskManagerSyncedWindow.OnDataChanged?.GetInvocationList().Length ?? 0;
                Debug.Log($"[DEBUG] Triggering UI Refresh. Event listeners: {listenerCount}");
                
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
                Debug.Log($"🏃 Client {senderId} manually left. Removed from registry.");
            }
        }

        public static void CleanupTimedOutClients()
        {
            var taskData = UnityEngine.Resources.Load<TaskData>("TaskData");
            if (taskData == null || taskData.ActiveClients.Count == 0) return;

            bool changed = false;
            DateTime now = DateTime.Now;

            for (int i = 0; i < taskData.ActiveClients.Count; i++)
            {
                var client = taskData.ActiveClients[i];
                if (client.SenderId == "editor") continue; // Never timeout host

                if (DateTime.TryParse(client.LastSeen, out DateTime lastSeenTime))
                {
                    if ((now - lastSeenTime).TotalSeconds > 60) // 1 minute timeout
                    {
                        Debug.Log($"⏳ Client {client.Name} timed out. Removing.");
                        taskData.ActiveClients.RemoveAt(i);
                        i--;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(taskData);
                AssetDatabase.SaveAssets();
                BroadcastClientList(taskData);
                
                int listenerCount = TaskManagerSyncedWindow.OnDataChanged?.GetInvocationList().Length ?? 0;
                Debug.Log($"[DEBUG] Timeout cleanup triggered UI Refresh. listeners: {listenerCount}");
                
                TaskManagerSyncedWindow.OnDataChanged?.Invoke();
            }
        }
    }
}
#endif
