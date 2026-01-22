using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Terminal
{
    public class TerminalWindow : EditorWindow
    {
        [SerializeField] private TerminalUI ui = new TerminalUI();

        [MenuItem("Tools/GameDevTools/Integrated Terminal %`", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<TerminalWindow>("Terminal");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            ui.Initialize();
        }

        private void OnDisable()
        {
            ui.OnDisable();
        }

        public void CreateNewSession(string path = null)
        {
            ui.CreateNewSession(path);
        }

        private void OnGUI()
        {
            ui.Draw(position);
        }

        private void Update()
        {
            ui.Update();
            // Terminal needs constant repaints for real-time output
            Repaint();
        }
    }
}

