using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.AdvancedInspector
{
    public class AdvancedInspectorWindow : EditorWindow
    {
        [SerializeField] private AdvancedInspectorUI ui = new AdvancedInspectorUI();

        [MenuItem("Tools/GameDevTools/Advanced Inspector", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdvancedInspectorWindow>("Advanced Inspector");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            ui.Initialize();
        }

        private void OnDisable()
        {
            ui.OnDisable();
        }

        private void OnGUI()
        {
            ui.UpdateSelection();
            ui.Draw(position);
        }
    }
}
