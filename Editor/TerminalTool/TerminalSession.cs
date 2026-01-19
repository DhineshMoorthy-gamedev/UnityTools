using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Terminal
{
    public enum ShellType
    {
        CMD,
        PowerShell
    }

    [Serializable]
    public class TerminalSession
    {
        public string SessionName;
        public string WorkingDirectory;
        public ShellType Shell;
        
        [SerializeField]
        private string currentDirectory = "";
        
        public string CurrentDirectory 
        { 
            get 
            {
                if (string.IsNullOrEmpty(currentDirectory))
                    return WorkingDirectory;
                return currentDirectory;
            }
        }
        
        [SerializeField]
        public int ProxyPort = -1;
        
        [SerializeField]
        public int ProxyPid = -1;

        [SerializeField]
        private string persistedOutput = "";
        
        // This is now purely tracking if *we* think it should be running
        [SerializeField]
        public bool IsActive = false;

        // Backward compatibility properties
        public bool WasRunning 
        { 
            get => IsActive; 
            set => IsActive = value; 
        }
        
        public bool IsRunning => IsActive;

        [NonSerialized]
        private TcpClient client;
        [NonSerialized]
        private NetworkStream stream;
        [NonSerialized]
        private Thread receiveThread;
        
        [NonSerialized]
        private StringBuilder outputBuffer;
        [NonSerialized]
        private Queue<string> outputQueue;
        [NonSerialized]
        private object lockObject;
        [NonSerialized]
        private List<string> history;
        private int historyIndex = -1;

        public delegate void OutputReceivedHandler(string data);
        public event OutputReceivedHandler OnOutputReceived;
        
        public string Output 
        {
            get 
            {
                CheckInitialization();
                return persistedOutput + outputBuffer.ToString();
            }
        }
        
        public bool IsConnected => client != null && client.Connected;

        public TerminalSession(string name, string workingDir, ShellType shell)
        {
            SessionName = name;
            WorkingDirectory = workingDir;
            Shell = shell;
            CheckInitialization();
        }

        private void CheckInitialization()
        {
            if (outputBuffer == null) outputBuffer = new StringBuilder();
            if (outputQueue == null) outputQueue = new Queue<string>();
            if (lockObject == null) lockObject = new object();
            if (history == null) history = new List<string>();
        }

        public void Start()
        {
            CheckInitialization();
            if (IsConnected) return;

            if (ProxyPort != -1)
            {
                if (TryConnect()) 
                {
                    IsActive = true;
                    return; 
                }
            }

            string proxyPath = TerminalProxyBuilder.BuildAndGetPath();
            if (string.IsNullOrEmpty(proxyPath)) return;

            ProxyPort = GetFreePort();

            string shellExe = Shell == ShellType.PowerShell ? "powershell.exe" : "cmd.exe";
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = proxyPath,
                Arguments = $"{ProxyPort} \"{shellExe}\" \"{WorkingDirectory}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var p = Process.Start(startInfo);
            if (p != null) ProxyPid = p.Id;
            
            Thread.Sleep(500);

            if (TryConnect())
            {
                IsActive = true;
            }
            else
            {
                AppendToOutput("Failed to connect to terminal backend.");
            }
        }

        private int GetFreePort()
        {
            TcpListener l = new TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private bool TryConnect()
        {
            try
            {
                client = new TcpClient();
                client.Connect("127.0.0.1", ProxyPort);
                stream = client.GetStream();
                
                receiveThread = new Thread(ReceiveLoop);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            while (client != null && client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Disconnected

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    AppendToOutput(data, false); // Don't verify thread here
                }
                catch
                {
                    break;
                }
            }
            // Connection lost
        }

        public void ExecuteCommand(string command)
        {
            CheckInitialization();
            if (!IsConnected) Start();

            if (IsConnected)
            {
                if (!string.IsNullOrEmpty(command))
                {
                    history.Add(command);
                    historyIndex = history.Count;
                }
                
                // If it's a cd command, append a directory query to update the prompt
                string commandToSend = command;
                string trimmedCmd = command.Trim().ToLower();
                if (trimmedCmd.StartsWith("cd ") || trimmedCmd == "cd" || trimmedCmd.StartsWith("cd..") || trimmedCmd.StartsWith("cd\\") || trimmedCmd.StartsWith("cd/"))
                {
                    // After cd, query the current directory
                    if (Shell == ShellType.PowerShell)
                    {
                        commandToSend = command + " ; (Get-Location).Path";
                    }
                    else // CMD
                    {
                        commandToSend = command + " & cd";
                    }
                }
                
                byte[] bytes = Encoding.UTF8.GetBytes(commandToSend + "\n");
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void SendControlC()
        {
             if (IsConnected)
            {
                byte[] bytes = new byte[] { 3 }; // ETX
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void AppendToOutput(string data, bool onMainThread = true)
        {
            if (data == null) return;
            if (onMainThread) CheckInitialization();
            
            lock (lockObject)
            {
                if (outputQueue == null && !onMainThread) return; // Should not happen if init checked
                outputQueue.Enqueue(data);
            }
        }

        public void Update()
        {
            CheckInitialization();
            lock (lockObject)
            {
                while (outputQueue.Count > 0)
                {
                    string data = outputQueue.Dequeue();
                    outputBuffer.Append(data);
                    
                    // Try to extract current directory from output
                    TryExtractDirectory(data);
                    
                    OnOutputReceived?.Invoke(data);
                }
            }
        }

        private void TryExtractDirectory(string output)
        {
            if (string.IsNullOrEmpty(output)) return;
            
            // PowerShell prompt pattern: PS C:\path>
            if (output.Contains("PS ") && output.Contains(">"))
            {
                int psIndex = output.IndexOf("PS ");
                int gtIndex = output.IndexOf(">", psIndex);
                if (gtIndex > psIndex + 3)
                {
                    string path = output.Substring(psIndex + 3, gtIndex - psIndex - 3).Trim();
                    if (Directory.Exists(path))
                    {
                        currentDirectory = path;
                    }
                }
            }
            // CMD prompt pattern: C:\path>
            else if (output.Contains(">") && output.Length > 3)
            {
                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 3 && trimmed.Contains(":") && trimmed.EndsWith(">"))
                    {
                        string path = trimmed.Substring(0, trimmed.Length - 1).Trim();
                        if (Directory.Exists(path))
                        {
                            currentDirectory = path;
                        }
                    }
                }
            }
            // Plain path output (from our injected pwd/cd commands)
            else
            {
                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    // Check if it looks like a valid path (contains : and is a valid directory)
                    if (trimmed.Length > 3 && trimmed.Contains(":") && !trimmed.Contains(">") && Directory.Exists(trimmed))
                    {
                        currentDirectory = trimmed;
                        break;
                    }
                }
            }
        }

        public void ReloadPersistence()
        {
            CheckInitialization();
            persistedOutput += outputBuffer.ToString();
            outputBuffer.Clear();
            Disconnect();
        }

        private void Disconnect()
        {
            if (client != null)
            {
                try { client.Close(); } catch {}
                client = null;
            }
        }

        public void Stop()
        {
            Disconnect();
            IsActive = false;

            if (ProxyPid != -1)
            {
                try
                {
                    using (var killer = new Process())
                    {
                        killer.StartInfo.FileName = "taskkill";
                        killer.StartInfo.Arguments = $"/F /T /PID {ProxyPid}";
                        killer.StartInfo.CreateNoWindow = true;
                        killer.StartInfo.UseShellExecute = false;
                        killer.Start();
                        killer.WaitForExit(1000);
                    }
                }
                catch (Exception ex) 
                {
                    UnityEngine.Debug.LogWarning($"Failed to kill proxy: {ex.Message}");
                }
                ProxyPid = -1;
            }
            ProxyPort = -1;
        }

        public void Restart()
        {
            AppendToOutput("\n[Restarting terminal...]\n");
            
            // Update the working directory to the current directory before restart
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                WorkingDirectory = currentDirectory;
            }
            
            Stop();
            Thread.Sleep(1000); // Give the process time to fully terminate
            Start();
            AppendToOutput("[Terminal restarted successfully]\n");
        }

        public void Clear()
        {
            CheckInitialization();
            outputBuffer.Clear();
            persistedOutput = "";
        }

        private string stashedInput = "";

        public string GetHistoryUp(string currentInput)
        {
            CheckInitialization();
            if (history.Count == 0) return currentInput;
            
            if (historyIndex == history.Count)
            {
                stashedInput = currentInput;
            }

            historyIndex = Mathf.Clamp(historyIndex - 1, 0, history.Count - 1);
            return history[historyIndex];
        }

        public string GetHistoryDown()
        {
            CheckInitialization();
            if (history.Count == 0) return stashedInput;
            
            historyIndex++;
            if (historyIndex >= history.Count)
            {
                historyIndex = history.Count;
                return stashedInput;
            }
            return history[historyIndex];
        }
    }
}
