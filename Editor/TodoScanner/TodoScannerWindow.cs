using UnityEditor;
using UnityEngine;

namespace GameDevTools.Editor
{
    public class TodoScannerWindow : EditorWindow
    {
        [SerializeField] private TodoScannerUI ui = new TodoScannerUI();

        [MenuItem("Tools/GameDevTools/TODO Scanner", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<TodoScannerWindow>("TODO Scanner");
            window.minSize = new Vector2(400, 300);
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
