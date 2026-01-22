using UnityEditor;
using UnityEngine;

namespace GameDevTools.Editor
{
    public class NoteDashboardWindow : EditorWindow
    {
        [SerializeField] private NoteDashboardUI ui = new NoteDashboardUI();

        [MenuItem("Tools/GameDevTools/Note Dashboard", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<NoteDashboardWindow>("Note Dashboard");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            ui.Initialize();
        }

        private void OnFocus()
        {
            ui.OnFocus();
        }

        private void OnGUI()
        {
            ui.Draw();
        }

        private void OnHierarchyChange()
        {
            ui.RefreshNotes();
            Repaint();
        }
    }
}
