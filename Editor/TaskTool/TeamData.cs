using System.Collections.Generic;
using UnityEngine;

namespace UnityProductivityTools.TaskTool.Editor
{
    // Stored in Editor resources or alongside TaskData
    public class TeamData : ScriptableObject
    {
        public List<string> Members = new List<string>();
    }
}
