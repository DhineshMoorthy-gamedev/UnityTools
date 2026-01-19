using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Terminal
{
    public static class TerminalProxyBuilder
    {
        private static string ProxySource = @"
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TerminalProxy
{
    static Process shellProcess;
    static TcpListener server;
    static TcpClient client;
    static NetworkStream stream;
    static bool isRunning = true;
    static DateTime lastConnectionTime;

    static void Main(string[] args)
    {
        int port = int.Parse(args[0]);
        string shell = args[1];
        string workingDir = args.Length > 2 ? args[2] : Directory.GetCurrentDirectory();

        // Start TCP Server
        server = new TcpListener(IPAddress.Loopback, port);
        server.Start();

        // Start Shell
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = shell,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (shell.ToLower().Contains(""cmd"")) startInfo.Arguments = ""/K"";
        if (shell.ToLower().Contains(""powershell"")) startInfo.Arguments = ""-NoExit"";

        shellProcess = new Process { StartInfo = startInfo };
        shellProcess.OutputDataReceived += (s, e) => SendToClient(e.Data);
        shellProcess.ErrorDataReceived += (s, e) => SendToClient(e.Data);
        
        try
        {
            shellProcess.Start();
            shellProcess.BeginOutputReadLine();
            shellProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(""Failed to start shell: "" + ex.Message);
            return;
        }

        lastConnectionTime = DateTime.Now;

        // Monitor connection and cleanup thread
        new Thread(MonitorLoop).Start();

        while (isRunning && !shellProcess.HasExited)
        {
            try
            {
                if (server.Pending())
                {
                    client = server.AcceptTcpClient();
                    stream = client.GetStream();
                    lastConnectionTime = DateTime.Now;

                    byte[] buffer = new byte[1024];
                    while (client.Connected)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string input = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        shellProcess.StandardInput.Write(input);
                        lastConnectionTime = DateTime.Now;
                    }
                    client.Close();
                    client = null;
                }
                Thread.Sleep(100);
            }
            catch {}
        }

        if (!shellProcess.HasExited)
        {
             // Kill process tree
             Process.Start(new ProcessStartInfo
             {
                 FileName = ""taskkill"",
                 Arguments = string.Format(""/F /T /PID {0}"", shellProcess.Id),
                 CreateNoWindow = true,
                 UseShellExecute = false
             }).WaitForExit();
        }
    }

    static void MonitorLoop()
    {
        while (isRunning)
        {
            // If no client for 60 seconds, shutdown to prevent zombies
            if (client == null && (DateTime.Now - lastConnectionTime).TotalSeconds > 60)
            {
                isRunning = false;
                if (!shellProcess.HasExited) shellProcess.Kill();
                Environment.Exit(0);
            }
            Thread.Sleep(1000);
        }
    }

    static void SendToClient(string data)
    {
        if (data == null) return;
        try 
        {
            if (client != null && client.Connected && stream != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data + ""\n"");
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch {}
    }
}
";

        public static string BuildAndGetPath()
        {
            string tempDir = Path.GetFullPath("Temp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string buildPath = Path.Combine(tempDir, "TerminalProxy.exe");
            string sourcePath = Path.Combine(tempDir, "TerminalProxy.cs");

            // If the proxy already exists, just return it (avoid rebuild while it's running)
            if (File.Exists(buildPath))
            {
                return buildPath;
            }

            File.WriteAllText(sourcePath, ProxySource);

            ProcessStartInfo csc = new ProcessStartInfo
            {
                FileName = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
                Arguments = $"/target:exe /out:\"{buildPath}\" \"{sourcePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            var p = Process.Start(csc);
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                UnityEngine.Debug.LogError("Failed to build TerminalProxy: " + p.StandardOutput.ReadToEnd());
                return null;
            }

            return Path.GetFullPath(buildPath);
        }
    }
}
