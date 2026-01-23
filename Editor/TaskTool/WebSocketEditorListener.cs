#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using UnityProductivityTools.TaskTool;

namespace UnityProductivityTools.TaskTool.Editor
{
    [InitializeOnLoad]
    public static class WebSocketEditorListener
    {
        static ClientWebSocket socket;
        static CancellationTokenSource cts;
        static ConcurrentQueue<string> messageQueue = new();

        static string serverIp = "127.0.0.1";
        static int port = 8080;
        static TaskToolEnvironment TaskToolEnvironment = TaskToolEnvironment.Local;
        static string serverUrl = "wss://node-server-ws.onrender.com";
        static bool connected = false;
        static string sessionId = Guid.NewGuid().ToString(); // Unique ID for this editor session
        public static string CurrentProjectName = ""; // Project identifier

        public static bool IsConnected => connected && socket != null && socket.State == WebSocketState.Open;
        public static string SocketStatus => socket == null ? "NULL" : socket.State.ToString();
        static bool isConnecting = false;

        public static event Action OnConnected;

        static WebSocketEditorListener()
        {
            Debug.Log("🧠 [WS] Editor WebSocket Listener Initialized");
            Connect();
            EditorApplication.update += Update;
        }

        public static void Reconnect()
        {
            Debug.Log("🔄 [WS] Manual Reconnect Requested...");
            Connect();
        }

        public static async void Disconnect()
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                try
                {
                    Debug.Log("🔌 [WS] Manual Disconnect Requested...");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User Disconnect", CancellationToken.None);
                    socket.Dispose();
                    socket = null;
                    connected = false;
                    Debug.Log("✅ [WS] Disconnected.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"⚠ [WS] Error during disconnect: {e.Message}");
                }
            }
        }

        static async void Connect()
        {
            if (isConnecting) return;
            isConnecting = true;
            connected = false;

            if (socket != null)
            {
                try 
                { 
                    if (socket.State == WebSocketState.Open)
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                    socket.Dispose(); 
                } catch { }
            }

            socket = new ClientWebSocket();
            cts = new CancellationTokenSource();

            LoadSettings(); // Reload settings before connecting
            var uri = default(Uri);
            if (TaskToolEnvironment == TaskToolEnvironment.Remote)
            {
                serverUrl = "wss://node-server-ws.onrender.com";
                uri = new Uri(serverUrl);
            }
            else
            {
                //serverUrl = $"ws://{serverIp}:{port}";
                uri = new Uri($"ws://{serverIp}:{port}");
            }
            //var uri = new Uri($"ws://{serverIp}:{port}");
            //var uri = new Uri(serverUrl);

            Debug.Log($"🔌 [WS] Attempting bridge to {uri} (Current State: {SocketStatus})");

            try
            {
                using (var timeoutCts = new CancellationTokenSource(5000))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token))
                {
                    await socket.ConnectAsync(uri, linkedCts.Token);
                }

                connected = true;
                Debug.Log("✅ [WS] Editor WebSocket CONNECTED");
                
                // Send Identity Handshake
                Send(new WSMessage { 
                    sender = "editor", 
                    type = "identity", 
                    payload = "Unity Editor Connected",
                    projectId = string.IsNullOrEmpty(CurrentProjectName) ? Application.productName : CurrentProjectName
                });

                OnConnected?.Invoke();
                _ = ReceiveLoop();
            }
            catch (Exception e)
            {
                connected = false;
                Debug.LogWarning($"❌ [WS] Connection failed: {e.Message}");
            }
            finally
            {
                isConnecting = false;
            }
        }

        static async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string msg = Encoding.UTF8.GetString(ms.ToArray());
                    messageQueue.Enqueue(msg);
                }
            }
            catch (Exception e)
            {
                if (socket.State != WebSocketState.Aborted)
                    Debug.LogWarning("[WS] Receive Loop stopped: " + e.Message);
            }
            finally
            {
                connected = false;
                Debug.Log("🔌 [WS] Editor WebSocket DISCONNECTED");
            }
        }

        public static async void Send(WSMessage msg)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[WS] Cannot send: State is {SocketStatus}. Attempting reconnect...");
                Connect();
                return;
            }

            try
            {
                msg.senderId = sessionId; // Attach our unique ID
                msg.projectId = string.IsNullOrEmpty(CurrentProjectName) ? Application.productName : CurrentProjectName;
                string json = JsonUtility.ToJson(msg);
                byte[] data = Encoding.UTF8.GetBytes(json);

                await socket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Text,
                    true,
                    cts.Token
                );
                Debug.Log($"📤 [WS] Sent {msg.type} ({data.Length} bytes)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WS] Send Failed: {e.Message}");
                connected = false;
            }
        }

        static void Update()
        {
            if (messageQueue.Count == 0) return;

            while (messageQueue.TryDequeue(out string msg))
            {
                try
                {
                    var data = JsonUtility.FromJson<WSMessage>(msg);
                    if (data == null) continue;

                    // Ignore our own messages reflected from server (check Session ID)
                    if (data.senderId == sessionId) continue;

                    Debug.Log($"📩 [WS] Received {data.type} from {data.sender} ({data.senderId})");
                    EditorCommandHandler.Handle(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WS] Failed to parse message: {e.Message}\nRaw: {msg}");
                }
            }
        }
        static void LoadSettings()
        {
            var guids = AssetDatabase.FindAssets("t:TaskToolSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var settings = AssetDatabase.LoadAssetAtPath<TaskToolSettings>(path);
                if (settings != null)
                {
                    serverIp = settings.ServerIP;
                    port = settings.ServerPort;
                    TaskToolEnvironment = settings.Environment;
                    CurrentProjectName = settings.ProjectName;
                    // Debug.Log($"⚙ [WS] Loaded Settings: {serverIp}:{port}");
                }
            }
        }
    }
}
#endif
