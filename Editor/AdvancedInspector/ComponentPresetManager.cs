using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace UnityProductivityTools.AdvancedInspector
{
    public static class ComponentPresetManager
    {
        private const string PRESETS_FOLDER = "Assets/Editor/AdvancedInspector/Presets";

        public static void SavePreset(Component component)
        {
            if (component == null) return;

            if (!Directory.Exists(PRESETS_FOLDER))
            {
                Directory.CreateDirectory(PRESETS_FOLDER);
            }

            string fileName = $"{component.gameObject.name}_{component.GetType().Name}_{System.DateTime.Now:yyyyMMddHHmmss}.json";
            string path = Path.Combine(PRESETS_FOLDER, fileName);

            string json = EditorJsonUtility.ToJson(component, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();

            Debug.Log($"Preset saved to: {path}");
        }

        public static void ApplyPreset(Component targetComponent, string presetPath)
        {
            if (targetComponent == null || !File.Exists(presetPath)) return;

            string json = File.ReadAllText(presetPath);
            Undo.RecordObject(targetComponent, "Apply Component Preset");
            EditorJsonUtility.FromJsonOverwrite(json, targetComponent);
            
            Debug.Log($"Preset applied to: {targetComponent.name}");
        }

        public static string[] GetPresetsForComponent(string componentTypeName)
        {
            if (!Directory.Exists(PRESETS_FOLDER)) return new string[0];

            string[] files = Directory.GetFiles(PRESETS_FOLDER, $"*_{componentTypeName}_*.json");
            return files;
        }
    }
}
