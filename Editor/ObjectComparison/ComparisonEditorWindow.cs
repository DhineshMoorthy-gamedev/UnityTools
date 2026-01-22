using UnityEngine;
using UnityEditor;

namespace UnityTools.ObjectComparison
{
    public class ComparisonEditorWindow : EditorWindow
    {
        [SerializeField] private ComparisonUI ui = new ComparisonUI();

        [MenuItem("Tools/GameDevTools/Object Comparison", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<ComparisonEditorWindow>("Object Comparison");
        }

        private void OnEnable()
        {
            ui.Initialize();
        }

        private void OnGUI()
        {
            ui.Draw(position);
        }
    }
}
