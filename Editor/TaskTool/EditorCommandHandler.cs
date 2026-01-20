using UnityEngine;
using UnityEditor.Build.Reporting;

using System;
using System.IO;

using UnityProductivityTools.TaskTool;

#if UNITY_EDITOR
using UnityEditor;

namespace UnityProductivityTools.TaskTool.Editor
{
    public static class EditorCommandHandler
    {
        public static void Handle(WSMessage msg)
        {
            if (msg.sender != "mobile" && msg.sender != "editor")
                return;

            switch (msg.type)
            {
                case "command":
                    if (msg.payload == "Start Build") StartBuild();
                    else if (msg.payload == "Play Mode On") EnterPlayMode();
                    else if (msg.payload == "Play Mode Off") ExitPlayMode();
                    break;

                case "task_sync":
                    SyncTasks(msg.payload);
                    break;

                case "request_sync":
                    SendCurrentTaskList();
                    break;

                default:
                    Debug.LogWarning("❓ Unknown message type: " + msg.type);
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

        static void SendCurrentTaskList()
        {
            Debug.Log("📤 Mobile client requested initial task sync");
            
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
                    payload = json
                };
                
                WebSocketEditorListener.Send(msg);
                int taskCount = taskData.Tasks != null ? taskData.Tasks.Count : 0;
                Debug.Log($"✅ Sent {taskCount} tasks to mobile client");
            }
            else
            {
                Debug.LogWarning("⚠ No TaskData found to send");
            }
        }

        static void SyncTasks(string json)
        {
            Debug.Log("🔄 Syncing Tasks from Mobile...");
            var window = EditorWindow.GetWindow<TaskManagerSyncedWindow>();
            if (window != null)
            {
                window.UpdateData(json);
            }
        }
    }
}
#endif
