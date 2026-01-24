using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace UnityProductivityTools.TaskTool
{
    public class WebSocketClient : MonoBehaviour
    {
        public enum environment
        {
            Local,
            Remote
        }
        public environment env;
        public TaskToolSettings settings; // Optional: Link the settings asset
        public string serverIp = "192.168.1.100"; // LAN IP
        public int port = 8080;
        static string serverUrl = "wss://node-server-ws.onrender.com";
        [SerializeField]
        private TaskData _syncedTasks;
        private Vector2 _scrollPos;

        public string currentProjectId = ""; // ID of the linked project
        private HashSet<string> _discoveredProjectIds = new();
        private Vector2 _projectsScrollPos;
        private bool _showProjectSelector = false;
        
        ClientWebSocket socket;
        CancellationTokenSource cts;
        ConcurrentQueue<string> messageQueue = new();

        public bool autoReconnect = false; // manual only for now
        bool isConnecting = false;

        public static Action NotifyTaskDataChanged;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(currentProjectId))
                currentProjectId = Application.productName;

            if (_syncedTasks == null) _syncedTasks = ScriptableObject.CreateInstance<TaskData>();
            NotifyTaskDataChanged += taskadded;
        }

        private void OnDisable()
        {
            NotifyTaskDataChanged -= taskadded;
        }

        public async void connectui()
        {
            await Connect();
        }

        [ContextMenu("Connect")]
        async Task Connect()
        {
            socket = new ClientWebSocket();
            cts = new CancellationTokenSource();

            if (settings != null)
            {
                serverIp = settings.ServerIP;
                port = settings.ServerPort;
            }

            var uri  = new Uri(serverUrl);
            if (env == environment.Remote)
            {
                //serverUrl = "wss://node-server-ws.onrender.com";
                uri = new Uri(serverUrl);
            }
            else
            {
                //serverUrl = $"ws://{serverIp}:{port}";
                uri = new Uri($"ws://{serverIp}:{port}");
            }
            //var uri = new Uri($"wss://{serverIp}:{port}");
            //var uri = new Uri(serverUrl);
            Debug.Log("🔌 Trying to connect to: " + uri);

            try
            {
                await socket.ConnectAsync(uri, cts.Token);
                Debug.Log("✅ WebSocket CONNECTED");
                _ = ReceiveLoop();
                
                // Request initial task sync from Editor
                await System.Threading.Tasks.Task.Delay(500); // Small delay to ensure connection is stable
                Send(new WSMessage
                {
                    sender = "mobile",
                    type = "request_sync",
                    payload = "Initial sync request"
                });
                Debug.Log("📨 Requested initial task sync from Editor");
            }
            catch (Exception e)
            {
                Debug.LogError("❌ WebSocket FAILED: " + e);
            }
        }


        async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            try
            {
                Debug.Log("📡 [Mobile] ReceiveLoop started");
                while (socket.State == WebSocketState.Open)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.LogWarning("⚠ WebSocket closed by server");
                        break;
                    }

                    string msg = Encoding.UTF8.GetString(ms.ToArray());
                    Debug.Log($"📥 [Mobile] Received raw message ({msg.Length} bytes), queuing...");
                    messageQueue.Enqueue(msg);
                    Debug.Log($"📊 [Mobile] Queue size: {messageQueue.Count}");
                }
            }
            catch (Exception e)
            {
                if (socket.State != WebSocketState.Aborted)
                    Debug.LogWarning("⚠ WebSocket receive stopped: " + e.Message);
            }

            Debug.Log("🔌 WebSocket disconnected");
        }


        public async void Send(WSMessage msg)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;

            msg.projectId = currentProjectId; // Always attach project ID
            string json = JsonUtility.ToJson(msg);
            byte[] data = Encoding.UTF8.GetBytes(json);

            await socket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Text,
                true,
                cts.Token
            );
        }

        void Update()
        {
            int processedCount = 0;
            while (messageQueue.TryDequeue(out string msg))
            {
                processedCount++;
                Debug.Log($"🔄 [Mobile] Processing message {processedCount} from queue");
                var data = JsonUtility.FromJson<WSMessage>(msg);
                OnMessage(data);
            }
            
            if (processedCount > 0)
            {
                Debug.Log($"✅ [Mobile] Processed {processedCount} messages this frame");
            }
        }
        public static event Action<WSMessage> OnMessageReceived;

        void OnMessage(WSMessage msg)
        {
            // Project Discovery: Track any project IDs we see (from anyone)
            if (!string.IsNullOrEmpty(msg.projectId))
            {
                if (!_discoveredProjectIds.Contains(msg.projectId))
                {
                    _discoveredProjectIds.Add(msg.projectId);
                    Debug.Log($"✨ Discovered new Project ID: {msg.projectId}");
                }
            }

            // Filter by Project ID: Only process if it matches or if it's a global command
            if (!string.IsNullOrEmpty(msg.projectId) && msg.projectId != currentProjectId)
            {
                // Debug.Log($"⏭ Ignoring message for project: {msg.projectId} (Current: {currentProjectId})");
                return;
            }

            if (msg.type == "task_sync")
            {
                HandleTaskSync(msg.payload);
            }

            OnMessageReceived?.Invoke(msg);
        }

        void HandleTaskSync(string json)
        {
            Debug.Log("🔄 Received Task Sync from Editor");
            if (_syncedTasks == null) _syncedTasks = ScriptableObject.CreateInstance<TaskData>();
            
            JsonUtility.FromJsonOverwrite(json, _syncedTasks);
            
            // Log the sync details
            int taskCount = _syncedTasks.Tasks != null ? _syncedTasks.Tasks.Count : 0;
            Debug.Log($"✅ Synced {taskCount} tasks successfully");
            
            // Force GUI to repaint immediately (no delay)
            // This ensures the OnGUI updates right away instead of waiting for the next frame
        }

        private void OnGUI()
        {
            float scale = Screen.dpi / 96.0f; // Scale for mobile high-DPI screens
            if (scale < 1) scale = 1;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUILayout.BeginArea(new Rect(10, 10, width - 20, height - 20));
            
            GUILayout.Label("📱 Mobile Task Manager", GUI.skin.FindStyle("Box"));

            if (socket == null || socket.State != WebSocketState.Open)
            {
                DrawConnectionScreen();
            }
            else
            {
                DrawSyncedTasksScreen();
            }

            GUILayout.EndArea();
        }

        private void DrawConnectionScreen()
        {
            GUILayout.Label("Server Configuration:", GetBoldLabelStyle());
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(40));
            serverIp = GUILayout.TextField(serverIp, GUILayout.ExpandHeight(true));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", GUILayout.Width(40));
            string portStr = GUILayout.TextField(port.ToString(), GUILayout.ExpandHeight(true));
            if (int.TryParse(portStr, out int newPort)) port = newPort;
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Project:", GUILayout.Width(60));
            currentProjectId = GUILayout.TextField(currentProjectId, GUILayout.ExpandHeight(true));
            if (GUILayout.Button("▼", GUILayout.Width(30))) _showProjectSelector = !_showProjectSelector;
            GUILayout.EndHorizontal();

            if (_showProjectSelector && _discoveredProjectIds.Count > 0)
            {
                _projectsScrollPos = GUILayout.BeginScrollView(_projectsScrollPos, "box", GUILayout.Height(100));
                foreach (var id in _discoveredProjectIds)
                {
                    if (GUILayout.Button(id, GUI.skin.button))
                    {
                        currentProjectId = id;
                        _showProjectSelector = false;
                    }
                }
                GUILayout.EndScrollView();
            }
            
            GUILayout.Space(10);
            
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Connect to Server", GUILayout.Height(60)))
            {
                connectui();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSyncedTasksScreen()
        {
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            GUILayout.Label($"● Connected: {currentProjectId}", GetMiniLabelStyle(), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnect", CancellationToken.None);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Manual Sync", GUILayout.Height(30)))
            {
                Send(new WSMessage { sender = "mobile", type = "request_sync", payload = "Manual sync" });
            }
            if (GUILayout.Button("Clear Data", GUILayout.Height(30)))
            {
                if (_syncedTasks != null) 
                {
                    _syncedTasks.Tasks.Clear();
                    NotifyTaskDataChanged?.Invoke();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.Space(10);

            if (_syncedTasks != null && _syncedTasks.Tasks != null)
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos);
                
                for (int i = 0; i < _syncedTasks.Tasks.Count; i++)
                {
                    var task = _syncedTasks.Tasks[i];
                    GUILayout.BeginVertical("box");
                    
                    // Header: Title + Status + Priority
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{task.Title}</b>", GetRichTextStyle(), GUILayout.ExpandWidth(true));
                    GUILayout.Label($"[{task.Status}]", GUILayout.Width(80));
                    GUILayout.EndHorizontal();

                    // Sub-header: Priority + Date
                    GUILayout.BeginHorizontal();
                    GUI.color = GetPriorityColor(task.Priority);
                    GUILayout.Label($"Priority: {task.Priority}", GetMiniLabelStyle());
                    GUI.color = Color.white;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(task.CreatedDate, GetMiniLabelStyle());
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2);

                    // Body: Description
                    if (!string.IsNullOrEmpty(task.Description))
                    {
                        GUILayout.Label("Description:", GetBoldLabelStyle());
                        GUILayout.Label(task.Description, GUI.skin.textArea);
                    }

                    GUILayout.Space(2);

                    // Links
                    if (task.Links != null && task.Links.Count > 0)
                    {
                        GUILayout.Space(5);
                        foreach (var link in task.Links)
                        {
                            if (string.IsNullOrEmpty(link.ObjectName)) continue;

                            GUI.backgroundColor = new Color(0.9f, 0.9f, 1f);
                            GUILayout.BeginVertical(GetHelpBoxStyle());
                            GUI.backgroundColor = Color.white;
                            
                            GUILayout.BeginHorizontal();
                            string linkLabel = $"<b>{link.ObjectName}</b>\n<size=9>{link.ObjectType}</size>";
                            if (!string.IsNullOrEmpty(link.ScenePath))
                            {
                                string sceneName = Path.GetFileNameWithoutExtension(link.ScenePath);
                                linkLabel += $" <color=#77aaff>@ {sceneName}</color>";
                            }
                            GUILayout.Label(linkLabel, GetRichTextStyle(), GUILayout.ExpandWidth(true));
                            
                            if (!string.IsNullOrEmpty(link.GlobalObjectId))
                            {
                                if (GUILayout.Button("PING", GUILayout.Width(60), GUILayout.Height(35)))
                                {
                                    Send(new WSMessage { 
                                        sender = "mobile", 
                                        type = "ping_object", 
                                        payload = link.GlobalObjectId,
                                        scenePath = link.ScenePath
                                    });
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                    }

                    // Footer: Assignee / Assigner
                    GUILayout.Space(2);
                    GUILayout.BeginHorizontal();
                    if (!string.IsNullOrEmpty(task.Assignee)) GUILayout.Label($"👤 {task.Assignee}", GetMiniLabelStyle());
                    if (!string.IsNullOrEmpty(task.Assigner)) GUILayout.Label($"✍ {task.Assigner}", GetMiniLabelStyle());
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }

                if (_syncedTasks.Tasks.Count == 0)
                {
                    GUILayout.Label("No tasks synced yet.");
                }

                GUILayout.EndScrollView();
            }
        }

        private Color GetPriorityColor(TaskPriority priority)
        {
            switch (priority)
            {
                case TaskPriority.Low: return Color.gray;
                case TaskPriority.Medium: return new Color(0.2f, 0.6f, 1f); // Blueish
                case TaskPriority.High: return new Color(1f, 0.6f, 0f); // Orange
                case TaskPriority.Critical: return Color.red;
                default: return Color.white;
            }
        }

        private GUIStyle GetRichTextStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            return style;
        }

        private GUIStyle GetMiniLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 10;
            style.alignment = TextAnchor.MiddleLeft;
            return style;
        }

        private GUIStyle GetBoldLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        private GUIStyle GetHelpBoxStyle()
        {
            GUIStyle style = GUI.skin.FindStyle("helpbox");
            if (style == null)
            {
                // Fallback for runtime
                style = new GUIStyle(GUI.skin.box);
                style.padding = new RectOffset(10, 10, 5, 5);
            }
            return style;
        }


        async void OnDestroy()
        {
            if (socket != null)
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
        }
        
        public void s()
        {
            Send(new WSMessage
            {
                sender = "mobile",
                type = "command",
                payload = "Start Build"
            });

        }

        public void enterplaymode()
        {
            Send(new WSMessage
            {
                sender = "mobile",
                type = "command",
                payload = "Play Mode On"
            });
        }

        public void exitplaymode()
        {
            Send(new WSMessage
            {
                sender = "mobile",
                type = "command",
                payload = "Play Mode Off"
            });
        }

        public void taskadded()
        {
            // This is a placeholder for sending a single task addition event
            // In a real scenario, you'd probably send the whole list or a specific task object.
            Send(new WSMessage
            {
                sender = "mobile",
                type = "task_sync",
                payload = "New Task Added" // Example payload
            });
        }
        public async void Reconnect()
        {
            if (isConnecting)
            {
                Debug.Log("🔄 Already trying to reconnect...");
                return;
            }

            isConnecting = true;

            try
            {
                Debug.Log("🔁 Reconnecting...");

                if (socket != null)
                {
                    if (socket.State == WebSocketState.Open ||
                        socket.State == WebSocketState.Connecting)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Reconnecting",
                            CancellationToken.None);
                    }

                    socket.Dispose();
                }

                cts?.Cancel();
                cts = new CancellationTokenSource();

                socket = new ClientWebSocket();

                var uri = new Uri($"ws://{serverIp}:{port}");
                await socket.ConnectAsync(uri, cts.Token);

                Debug.Log("✅ Reconnected successfully");

                _ = ReceiveLoop();
                
                // Request initial task sync from Editor after reconnect
                await System.Threading.Tasks.Task.Delay(500); 
                Send(new WSMessage
                {
                    sender = "mobile",
                    type = "request_sync",
                    payload = "Reconnect sync request"
                });
                Debug.Log("📨 Requested task sync (reconnect)");
            }
            catch (Exception e)
            {
                Debug.LogError("❌ Reconnect failed: " + e.Message);
            }
            finally
            {
                isConnecting = false;
            }
        }

    }
    [System.Serializable]
    public class WSMessage
    {
        public string sender;   // editor / mobile
        public string senderId; // Unique session ID
        public string targetId; // ID of the client this message is intended for
        public string type;     // log / command / status
        public string projectId; // ID of the project
        public string scenePath; // Path of the scene if applicable
        public string payload;
    }
}
