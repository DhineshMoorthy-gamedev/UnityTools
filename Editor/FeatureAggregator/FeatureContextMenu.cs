using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FeatureAggregator
{
    public static class FeatureContextMenu
    {
        [MenuItem("Assets/Feature Aggregator/Add Selected to Feature...", false, 20)]
        private static void AddToFeature()
        {
            GenericMenu menu = new GenericMenu();
            List<FeatureDefinition> features = FeatureManager.GetAllFeatures();

            if (features.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Features Found"));
            }

            foreach (var feature in features)
            {
                menu.AddItem(new GUIContent(feature.featureName), false, OnFeatureSelected, feature);
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Create New Feature..."), false, CreateNewFeature);

            menu.ShowAsContext();
        }

        [MenuItem("Assets/Feature Aggregator/Add Selected to Feature...", true)]
        private static bool ValidateAddToFeature()
        {
            return Selection.objects.Length > 0;
        }

        private static void OnFeatureSelected(object userData)
        {
            FeatureDefinition feature = (FeatureDefinition)userData;
            foreach (var obj in Selection.objects)
            {
                if (obj is MonoScript script)
                {
                    FeatureManager.AddScriptToFeature(feature, script);
                }
                else
                {
                    FeatureManager.AddAssetToFeature(feature, obj);
                }
            }
            Debug.Log($"Added {Selection.objects.Length} items to feature '{feature.featureName}'");
            
            // Refresh window if open
            if (EditorWindow.HasOpenInstances<FeatureAggregatorWindow>())
            {
                EditorWindow.GetWindow<FeatureAggregatorWindow>().Repaint();
            }
        }

        private static void CreateNewFeature()
        {
            FeatureAggregatorWindow.ShowWindow();
            // In a real implementation, we might want to trigger the "Create New" state directly,
            // but opening the window is a good MVP step.
        }
    }
}
