using UnityEditor;
using UnityEngine;

namespace UnityTools.ObjectGrouper.UI
{
    public class ObjectGrouperWindow : EditorWindow
    {
        [SerializeField] private ObjectGrouperUI ui = new ObjectGrouperUI();

        [MenuItem("Tools/GameDevTools/Object Grouper", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<ObjectGrouperWindow>("Object Grouper");
            window.Show();
        }

        private void OnEnable()
        {
            ui.Initialize();
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }
        
        private void OnSelectionChange() { Repaint(); }
        private void OnInspectorUpdate() { Repaint(); }

        private void OnGUI()
        {
            ui.Draw();
        }
    }
}
