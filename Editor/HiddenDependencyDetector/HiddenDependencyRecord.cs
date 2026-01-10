using UnityEngine;

namespace HiddenDeps
{
    public enum DependencyType
    {
        FindObjectOfType,
        GameObjectFind,
        GetComponent,
        GetComponentInChildren,
        SendMessage
    }

    [System.Serializable]
    public class HiddenDependencyRecord
    {
        public string scriptPath;
        public string scriptName;
        public int lineNumber;
        public DependencyType dependencyType;
        public string target;
        public string rawLine;
    }
}
