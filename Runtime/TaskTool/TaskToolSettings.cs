using UnityEngine;

namespace UnityProductivityTools.TaskTool
{
    [CreateAssetMenu(fileName = "TaskToolSettings", menuName = "Game Dev Tools/Task Tool Settings")]
    public class TaskToolSettings : ScriptableObject
    {
        [Header("Server Configuration")]
        [Tooltip("The IP address of the WebSocket server (e.g., 127.0.0.1 for local, or LAN IP for mobile)")]
        public string ServerIP;/* = "127.0.0.1";*/

        [Tooltip("The port of the WebSocket server")]
        public int ServerPort;/* = 8080;*/

        public string ProjectName;

        public TaskToolEnvironment Environment = TaskToolEnvironment.Local;
    }

    [System.Serializable]
    public enum TaskToolEnvironment
    {
        Local,
        Remote
    }
}
