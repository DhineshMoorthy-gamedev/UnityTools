using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.TaskTool.Editor
{
    public class TaskManagerWindow : EditorWindow
    {
        [SerializeField] private TaskManagerUI ui = new TaskManagerUI();

        [MenuItem("Tools/GameDevTools/Task Manager", false, 100)]
        public static void ShowWindow()
        {
            TaskManagerWindow window = GetWindow<TaskManagerWindow>("Task Manager");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            ui.Initialize();
        }

        private void OnGUI()
        {
            ui.Draw();
        }
    }
}

