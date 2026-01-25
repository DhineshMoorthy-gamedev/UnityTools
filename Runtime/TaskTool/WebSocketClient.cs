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
        private string _memberName = "";
        private string _joinCode = "";
        private string _sessionId = Guid.NewGuid().ToString();

        private enum MobileTab { Config, Join }
        private MobileTab _selectedTab = MobileTab.Join;

        // Modern UI Styles
        private GUIStyle _cardStyle, _primaryBtnStyle, _inputStyle, _headerStyle, _tabStyle, _tabSelectedStyle;
        private bool _stylesInitialized = false;

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

            // Initialize defaults from settings if available
            if (settings != null)
            {
                serverIp = settings.ServerIP;
                port = settings.ServerPort;
                env = (environment)settings.Environment;
            }

            if (_syncedTasks == null) _syncedTasks = ScriptableObject.CreateInstance<TaskData>();
            if (_syncedTasks.ActiveClients != null) _syncedTasks.ActiveClients.Clear();
            NotifyTaskDataChanged += taskadded;

            StartCoroutine(HeartbeatLoop());
        }

        private System.Collections.IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(20);
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    Send(new WSMessage { type = "heartbeat", payload = "ping" });
                }
            }
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

            var uri = new Uri(serverUrl);
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
                Debug.Log($"✅ WebSocket CONNECTED to {uri}");
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

            msg.senderId = _sessionId; // Attach unique session ID
            msg.projectId = currentProjectId; // Always attach project ID
            msg.platform = Application.platform.ToString();
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
            else if (msg.type == "editor_status")
            {
                if (msg.status == "offline")
                {
                    Debug.Log("🔌 Editor went offline. Clearing presence list.");
                    if (_syncedTasks != null && _syncedTasks.ActiveClients != null)
                    {
                        _syncedTasks.ActiveClients.Clear();
                    }
                }
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

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.9f, 0.9f, 1f) } };
            
            _cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(15, 15, 15, 15), margin = new RectOffset(5, 5, 10, 10) };
            
            _primaryBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, fixedHeight = 50 };
            
            _inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 40, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 5, 5) };

            _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 40 };
            _tabSelectedStyle = new GUIStyle(_tabStyle) { fontStyle = FontStyle.Bold, normal = { background = Texture2D.whiteTexture, textColor = Color.black } };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            float scale = Screen.dpi / 96.0f;
            if (scale < 1) scale = 1;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUILayout.BeginArea(new Rect(15, 15, width - 30, height - 30));

            // Modern Header
            GUILayout.Label("TASK MANAGER", _headerStyle);
            GUILayout.Space(10);

            if (socket == null || socket.State != WebSocketState.Open)
            {
                DrawTabHeader();
                GUILayout.BeginVertical("box");
                if (_selectedTab == MobileTab.Join) DrawJoinTab();
                else DrawConfigurationTab();
                GUILayout.EndVertical();
            }
            else
            {
                DrawSyncedTasksScreen();
            }

            GUILayout.EndArea();
        }

        private void DrawTabHeader()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("JOIN BY CODE", _selectedTab == MobileTab.Join ? _tabSelectedStyle : _tabStyle, GUILayout.ExpandWidth(true)))
                _selectedTab = MobileTab.Join;
            if (GUILayout.Button("SETTINGS", _selectedTab == MobileTab.Config ? _tabSelectedStyle : _tabStyle, GUILayout.ExpandWidth(true)))
                _selectedTab = MobileTab.Config;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawJoinTab()
        {
            GUILayout.Space(10);
            string targetDesc = env == environment.Remote ? "Cloud (SaaS)" : $"Local Server ({serverIp}:{port})";
            GUILayout.Label($"<b>Mode:</b> {targetDesc}", GetRichTextStyle());
            GUILayout.Label("Welcome! Enter your name and project invite code to join your team.", GetRichTextStyle());
            GUILayout.Space(15);

            GUILayout.Label("YOUR NAME", GetBoldLabelStyle());
            _memberName = GUILayout.TextField(_memberName, _inputStyle);
            
            GUILayout.Space(15);
            
            GUILayout.Label("INVITE CODE", GetBoldLabelStyle());
            _joinCode = GUILayout.TextField(_joinCode, _inputStyle);

            GUILayout.Space(30);

            GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
            if (GUILayout.Button("JOIN PROJECT", _primaryBtnStyle))
            {
                if (!string.IsNullOrEmpty(_memberName) && !string.IsNullOrEmpty(_joinCode))
                {
                    currentProjectId = _joinCode;
                    JoinAndConnect();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawConfigurationTab()
        {
            GUILayout.Space(10);
            GUILayout.Label("CONNECTION MODE", GetBoldLabelStyle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REMOTE (Cloud)", env == environment.Remote ? _tabSelectedStyle : _tabStyle, GUILayout.ExpandWidth(true)))
                env = environment.Remote;
            if (GUILayout.Button("LOCAL (LAN)", env == environment.Local ? _tabSelectedStyle : _tabStyle, GUILayout.ExpandWidth(true)))
                env = environment.Local;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            
            if (env == environment.Local)
            {
                GUILayout.Label("LOCAL SERVER SETTINGS", GetBoldLabelStyle());
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:", GUILayout.Width(50));
                serverIp = GUILayout.TextField(serverIp, _inputStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                GUILayout.Label("PORT:", GUILayout.Width(50));
                string portStr = GUILayout.TextField(port.ToString(), _inputStyle, GUILayout.ExpandWidth(true));
                if (int.TryParse(portStr, out int newPort)) port = newPort;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("CLOUD SERVER", GetBoldLabelStyle());
                GUILayout.Label("Using: " + serverUrl, GetMiniLabelStyle());
            }

            GUILayout.Space(15);

            GUILayout.Label("MANUAL PROJECT ID", GetBoldLabelStyle());
            GUILayout.BeginHorizontal();
            currentProjectId = GUILayout.TextField(currentProjectId, _inputStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("▼", GUILayout.Width(40), GUILayout.Height(40))) _showProjectSelector = !_showProjectSelector;
            GUILayout.EndHorizontal();

            if (_showProjectSelector && _discoveredProjectIds.Count > 0)
            {
                _projectsScrollPos = GUILayout.BeginScrollView(_projectsScrollPos, "box", GUILayout.Height(120));
                foreach (var id in _discoveredProjectIds)
                {
                    if (GUILayout.Button(id, GUI.skin.button, GUILayout.Height(40)))
                    {
                        currentProjectId = id;
                        _showProjectSelector = false;
                    }
                }
                GUILayout.EndScrollView();
            }
            
            GUILayout.Space(25);
            
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("CONNECT MANUALLY", _primaryBtnStyle))
            {
                connectui();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSyncedTasksScreen()
        {
            float scale = Screen.dpi / 96.0f;
            if (scale < 1) scale = 1;
            float width = Screen.width / scale;

            // Status Header
            GUILayout.BeginHorizontal(_cardStyle);
            GUI.color = Color.green;
            GUILayout.Label($"● {currentProjectId}", GetBoldLabelStyle());
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("DISCONNECT", GUI.skin.button, GUILayout.Width(90), GUILayout.Height(30)))
            {
                _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnect", CancellationToken.None);
            }
            GUILayout.EndHorizontal();

            // Member & Connection Summary
            if (_syncedTasks != null)
            {
                GUILayout.BeginVertical(_cardStyle);
                
                // Active Connections
                if (_syncedTasks.ActiveClients != null && _syncedTasks.ActiveClients.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    string countLabel = $"<b>Online ({_syncedTasks.ActiveClients.Count}):</b> ";
                    foreach (var client in _syncedTasks.ActiveClients)
                    {
                        string icon = GetPlatformIcon(client.Platform);
                        countLabel += $"{icon} ";
                    }
                    GUILayout.Label(countLabel, GetRichTextStyle());
                    GUILayout.EndHorizontal();
                }
                
                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REFRESH", GUILayout.Height(35), GUILayout.ExpandWidth(true)))
            {
                Send(new WSMessage { sender = "mobile", type = "request_sync", payload = "Manual sync" });
            }
            if (GUILayout.Button("CLEAR", GUILayout.Height(35), GUILayout.Width(70)))
            {
                if (_syncedTasks != null) 
                {
                    _syncedTasks.Tasks.Clear();
                    NotifyTaskDataChanged?.Invoke();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (_syncedTasks != null && _syncedTasks.Tasks != null)
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos);

                for (int i = 0; i < _syncedTasks.Tasks.Count; i++)
                {
                    var task = _syncedTasks.Tasks[i];
                    
                    // Card background based on priority/status
                    GUI.backgroundColor = task.Status == TaskStatus.Completed ? new Color(0.7f, 1f, 0.7f) : Color.white;
                    GUILayout.BeginVertical(_cardStyle);
                    GUI.backgroundColor = Color.white;

                    // Header: Title + Status
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<b>{task.Title}</b>", GetRichTextStyle(), GUILayout.ExpandWidth(true));
                    GUI.color = GetStatusColor(task.Status);
                    GUILayout.Label(task.Status.ToString().ToUpper(), GetBoldLabelStyle());
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    // Info Row
                    GUILayout.BeginHorizontal();
                    GUI.color = GetPriorityColor(task.Priority);
                    GUILayout.Label(task.Priority.ToString().ToUpper(), GetMiniLabelStyle());
                    GUI.color = Color.white;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(task.CreatedDate, GetMiniLabelStyle());
                    GUILayout.EndHorizontal();

                    GUILayout.Space(8);

                    // Body: Description
                    if (!string.IsNullOrEmpty(task.Description))
                    {
                        GUILayout.Label(task.Description, GUI.skin.label);
                    }

                    // Links
                    if (task.Links != null && task.Links.Count > 0)
                    {
                        GUILayout.Space(8);
                        foreach (var link in task.Links)
                        {
                            if (string.IsNullOrEmpty(link.ObjectName)) continue;

                            GUI.backgroundColor = new Color(0.9f, 0.94f, 1f);
                            GUILayout.BeginVertical(GUI.skin.box);
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
                                if (GUILayout.Button("FOCUS", GUILayout.Width(65), GUILayout.Height(35)))
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
                    GUILayout.Space(8);
                    
                    List<string> optionsList = new List<string>();
                    foreach (var client in _syncedTasks.ActiveClients)
                    {
                        if (!optionsList.Contains(client.Name)) optionsList.Add(client.Name);
                    }
                    string[] options = optionsList.ToArray();

                    if (options.Length > 0)
                    {
                        GUILayout.BeginHorizontal();
                        
                        // Assigner
                        GUILayout.BeginVertical(GUILayout.Width(width * 0.45f));
                        GUILayout.Label("✍ Assigner", GetMiniLabelStyle());
                        if (GUILayout.Button(string.IsNullOrEmpty(task.Assigner) ? "Unassigned" : task.Assigner, GUI.skin.button))
                        {
                             // Rotate through
                             int idx = Array.IndexOf(options, task.Assigner);
                             idx = (idx + 1) % (options.Length + 1);
                             task.Assigner = idx == options.Length ? "" : options[idx];
                             Send(new WSMessage { type = "task_sync", payload = JsonUtility.ToJson(_syncedTasks) });
                        }
                        GUILayout.EndVertical();

                        GUILayout.FlexibleSpace();

                        // Assignee
                        GUILayout.BeginVertical(GUILayout.Width(width * 0.45f));
                        GUILayout.Label("👤 Assignee", GetMiniLabelStyle());
                        if (GUILayout.Button(string.IsNullOrEmpty(task.Assignee) ? "Unassigned" : task.Assignee, GUI.skin.button))
                        {
                            // Rotate through
                            int idx = Array.IndexOf(options, task.Assignee);
                            idx = (idx + 1) % (options.Length + 1);
                            task.Assignee = idx == options.Length ? "" : options[idx];
                            Send(new WSMessage { type = "task_sync", payload = JsonUtility.ToJson(_syncedTasks) });
                        }
                        GUILayout.EndVertical();
                        
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (!string.IsNullOrEmpty(task.Assignee)) GUILayout.Label($"👤 {task.Assignee}", GetMiniLabelStyle());
                        GUILayout.FlexibleSpace();
                        if (!string.IsNullOrEmpty(task.Assigner)) GUILayout.Label($"✍ {task.Assigner}", GetMiniLabelStyle());
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }

                if (_syncedTasks.Tasks.Count == 0)
                {
                    GUILayout.Label("No tasks found for this project.", GetRichTextStyle());
                }

                GUILayout.EndScrollView();
            }
        }

        private Color GetStatusColor(TaskStatus status)
        {
            switch (status)
            {
                case TaskStatus.Pending: return Color.gray;
                case TaskStatus.InProgress: return new Color(1f, 0.8f, 0.2f);
                case TaskStatus.Completed: return new Color(0.2f, 0.8f, 0.2f);
                case TaskStatus.Blocked: return new Color(1f, 0.3f, 0.3f);
                default: return Color.white;
            }
        }

        private string GetPlatformIcon(string platform)
        {
            string p = platform.ToLower();
            if (p.Contains("android")) return "📱";
            if (p.Contains("iphone")) return "📱";
            if (p.Contains("ios")) return "📱";
            if (p.Contains("editor")) return "💻";
            if (p.Contains("win")) return "🖥️";
            if (p.Contains("mac")) return "🖥️";
            if (p.Contains("web")) return "🌐";
            if (p.Contains("html")) return "🌐";
            if (p.Contains("browser")) return "🌐";
            if (p.Contains("play")) return "🌐";
            return "❓";
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


        public async void Disconnect()
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                Send(new WSMessage { type = "member_leave", payload = _memberName });
                await System.Threading.Tasks.Task.Delay(100); // Give it a moment to send
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User Disconnect", CancellationToken.None);
                socket.Dispose();
                socket = null;
                Debug.Log("🔌 WebSocket disconnected gracefully");
            }
        }

        void SendIdentity()
        {
            if (string.IsNullOrEmpty(_memberName)) return;
            
            Send(new WSMessage { 
                type = "identity", 
                payload = _memberName, 
                sender = "mobile",
                platform = Application.platform.ToString()
            });
        }

        async void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                // Synchronous send attempt or just close
                Disconnect();
            }
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



        public async void JoinAndConnect()
        {
            await Connect();
            if (socket != null && socket.State == WebSocketState.Open)
            {
                Send(new WSMessage
                {
                    sender = "mobile",
                    type = "member_join",
                    payload = _memberName,
                    platform = Application.platform.ToString(),
                    projectId = currentProjectId
                });
                Debug.Log($"👥 Sent Join Request for {_memberName} to project {currentProjectId}");
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
        public string platform; // Android, Editor, WebGL etc
        public string projectId; // ID of the project
        public string scenePath; // Path of the scene if applicable
        public string payload;
        public string status;
    }

}