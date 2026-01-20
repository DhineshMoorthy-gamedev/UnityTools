using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProductivityTools.TaskTool
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
        public string Assignee;
        public string Assigner;
        public bool IsExpanded = true;
        
        public string CreatedDate;
        
        public TaskItem()
        {
            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }
    }

    [CreateAssetMenu(fileName = "TaskData", menuName = "TaskTool/TaskData")]
    public class TaskData : ScriptableObject
    {
        public List<TaskItem> Tasks = new List<TaskItem>();
    }
}
