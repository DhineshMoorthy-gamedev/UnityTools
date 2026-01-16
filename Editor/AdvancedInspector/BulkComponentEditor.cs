using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityProductivityTools.AdvancedInspector
{
    public class BulkComponentEditor : EditorWindow
    {
        private GameObject[] selectedObjects;
        private List<string> commonComponentTypes = new List<string>();
        private string selectedType;
        private Editor bulkEditor;
        private Vector2 scrollPos;

        public static void ShowWindow(GameObject[] objects)
        {
            var window = GetWindow<BulkComponentEditor>("Bulk Component Editor");
            window.selectedObjects = objects;
            window.RefreshCommonComponents();
        }

        private void RefreshCommonComponents()
        {
            if (selectedObjects == null || selectedObjects.Length == 0) return;

            var typeSets = selectedObjects.Select(go => go.GetComponents<Component>()
                .Select(c => c.GetType().FullName).ToHashSet()).ToList();

            if (typeSets.Count > 0)
            {
                var common = typeSets[0];
                for (int i = 1; i < typeSets.Count; i++)
                {
                    common.IntersectWith(typeSets[i]);
                }
                commonComponentTypes = common.ToList();
            }
        }

        private void OnGUI()
        {
            if (selectedObjects == null || selectedObjects.Length < 2)
            {
                EditorGUILayout.HelpBox("Select multiple GameObjects to use Bulk Edit.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Editing {selectedObjects.Length} GameObjects", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Component Type:", GUILayout.Width(100));
            int currentIndex = commonComponentTypes.IndexOf(selectedType);
            int newIndex = EditorGUILayout.Popup(currentIndex, commonComponentTypes.Select(t => t.Split('.').Last()).ToArray());
            if (newIndex != currentIndex && newIndex >= 0)
            {
                selectedType = commonComponentTypes[newIndex];
                UpdateBulkEditor();
            }
            EditorGUILayout.EndHorizontal();

            if (bulkEditor != null)
            {
                DrawMixedValueWarning();

                EditorGUILayout.Space(5);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                EditorGUI.BeginChangeCheck();
                bulkEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    // Changes handled by multi-object editor
                }
                EditorGUILayout.EndScrollView();
            }
            else if (!string.IsNullOrEmpty(selectedType))
            {
                EditorGUILayout.HelpBox("Could not create editor for selected type.", MessageType.Warning);
            }
        }

        private void DrawMixedValueWarning()
        {
            if (bulkEditor == null || bulkEditor.serializedObject == null) return;

            SerializedProperty prop = bulkEditor.serializedObject.GetIterator();
            List<string> mixedProps = new List<string>();

            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.hasMultipleDifferentValues)
                    {
                        mixedProps.Add(prop.displayName);
                    }
                } while (prop.NextVisible(false));
            }

            if (mixedProps.Count > 0)
            {
                EditorGUILayout.HelpBox($"Mixed Values detected in: {string.Join(", ", mixedProps)}", MessageType.Info);
            }
        }


        private void UpdateBulkEditor()
        {
            if (bulkEditor != null) DestroyImmediate(bulkEditor);
            
            var components = selectedObjects.SelectMany(go => go.GetComponents<Component>())
                .Where(c => c.GetType().FullName == selectedType).ToArray();

            if (components.Length > 0)
            {
                bulkEditor = Editor.CreateEditor(components);
            }
        }

        private void OnDisable()
        {
            if (bulkEditor != null) DestroyImmediate(bulkEditor);
        }
    }
}
