using UnityEditor;
using UnityEngine;

namespace HiddenDeps
{
    public class HiddenDependencyDetectorWindow : EditorWindow
    {
        [SerializeField] private HiddenDependencyDetectorUI ui = new HiddenDependencyDetectorUI();

        [MenuItem("Tools/GameDevTools/Hidden Dependency Detector", false, 100)]
        public static void Open()
        {
            GetWindow<HiddenDependencyDetectorWindow>("Hidden Dependencies");
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
