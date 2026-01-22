using UnityEngine;
using UnityEditor;

namespace FeatureAggregator
{
    public class FeatureAggregatorWindow : EditorWindow
    {
        [SerializeField] private FeatureAggregatorUI ui = new FeatureAggregatorUI();

        [MenuItem("Tools/GameDevTools/Feature Aggregator", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<FeatureAggregatorWindow>("Feature Aggregator");
        }

        private void OnEnable()
        {
            ui.Initialize(this);
        }
        
        private void OnFocus()
        {
             ui.OnFocus();
        }

        private void OnGUI()
        {
            ui.Draw();
        }
    }
}

