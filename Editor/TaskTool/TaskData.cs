using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProductivityTools.TaskTool.Editor
{
    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Blocked
    }

    [Serializable]
    public class TaskItem
    {
        public string Title;
        [TextArea(3, 10)]
        public string Description;
        public TaskPriority Priority = TaskPriority.Medium;
        public TaskStatus Status = TaskStatus.Pending;
        public string Owner;
        public bool IsExpanded = true;
        public string CreatedDate; // Added for tracking
        
        public TaskItem()
        {
            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }
    }

    // stored in Editor folder, so this is fine
    public class TaskData : ScriptableObject
    {
        public List<TaskItem> Tasks = new List<TaskItem>();
    }
}
