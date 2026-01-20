using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace FeatureAggregator
{
    public static class FeatureManager
    {
        private static string FeaturePath = "Assets/Editor/FeatureAggregator/Features";

        public static FeatureDefinition CreateFeature(string name, string description = "")
        {
            if (!Directory.Exists(FeaturePath))
            {
                Directory.CreateDirectory(FeaturePath);
            }

            FeatureDefinition newFeature = ScriptableObject.CreateInstance<FeatureDefinition>();
            newFeature.featureName = name;
            newFeature.description = description;

            string fileName = $"{name.Replace(" ", "_")}.asset";
            string path = Path.Combine(FeaturePath, fileName);
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(newFeature, path);
            AssetDatabase.SaveAssets();

            return newFeature;
        }

        public static List<FeatureDefinition> GetAllFeatures()
        {
            string[] guids = AssetDatabase.FindAssets("t:FeatureDefinition");
            List<FeatureDefinition> features = new List<FeatureDefinition>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                FeatureDefinition feature = AssetDatabase.LoadAssetAtPath<FeatureDefinition>(path);
                if (feature != null)
                {
                    features.Add(feature);
                }
            }
            return features;
        }

        public static void DeleteFeature(FeatureDefinition feature)
        {
            string path = AssetDatabase.GetAssetPath(feature);
            AssetDatabase.DeleteAsset(path);
        }

        public static void AddScriptToFeature(FeatureDefinition feature, MonoScript script)
        {
            if (!feature.relatedScripts.Contains(script))
            {
                feature.relatedScripts.Add(script);
                EditorUtility.SetDirty(feature);
                AssetDatabase.SaveAssets();
            }
        }
        
         public static void AddAssetToFeature(FeatureDefinition feature, Object asset)
        {
             if (!feature.relatedAssets.Contains(asset))
            {
                feature.relatedAssets.Add(asset);
                EditorUtility.SetDirty(feature);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
