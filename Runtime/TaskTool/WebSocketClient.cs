using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityProductivityTools.TaskTool
{
    public class WebSocketClient : MonoBehaviour
    {
        public TaskToolSettings settings; // Optional: Link the settings asset
        public string serverIp = "192.168.1.100"; // LAN IP
        public int port = 8080;
        static string serverUrl = "wss://node-server-ws.onrender.com";
        [SerializeField]
        private TaskData _syncedTasks;
        private Vector2 _scrollPos;

        ClientWebSocket socket;
        CancellationTokenSource cts;
        ConcurrentQueue<string> messageQueue = new();

        public bool autoReconnect = false; // manual only for now
        bool isConnecting = false;

        public static Action NotifyTaskDataChanged;

        private void OnEnable()
        {
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

            //var uri = new Uri($"wss://{serverIp}:{port}");
            var uri = new Uri(serverUrl);
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
            Debug.Log($"📩 [{msg.sender}] {msg.type}: {msg.payload}");

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
                GUILayout.Label("Server Configuration:", GetBoldLabelStyle());
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP Address:", GUILayout.Width(100));
                serverIp = GUILayout.TextField(serverIp, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("Port:", GUILayout.Width(100));
                string portStr = GUILayout.TextField(port.ToString(), GUILayout.ExpandWidth(true));
                if (int.TryParse(portStr, out int newPort))
                {
                    port = newPort;
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Connect to Server", GUILayout.Height(50)))
                {
                    connectui();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = Color.green;
                GUILayout.Label($"✅ Connected to {serverIp}:{port}", GUILayout.ExpandWidth(false));
                GUI.backgroundColor = Color.white;
                
                if (GUILayout.Button("Disconnect", GUILayout.Height(40)))
                {
                    _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnect", CancellationToken.None);
                }
                
                if (GUILayout.Button("Ping Editor", GUILayout.Height(40)))
                {
                    Send(new WSMessage { sender = "mobile", type = "status", payload = "Ping from Mobile" });
                }
            }

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

            GUILayout.EndArea();
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
        public string type;     // log / command / status
        public string payload;
    }
}
