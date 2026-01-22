using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Bootstrapper.Editor
{
    public class BootstrapperWindow : EditorWindow
    {
        [SerializeField] private BootstrapperUI ui = new BootstrapperUI();

        [MenuItem("Tools/GameDevTools/Project Bootstrapper", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<BootstrapperWindow>("Bootstrapper");
        }

        private void OnGUI()
        {
            ui.Draw();
        }
    }
}
