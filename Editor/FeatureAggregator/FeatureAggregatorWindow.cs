using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FeatureAggregator.Utils;
using System.Linq;

namespace FeatureAggregator
{
    public class FeatureAggregatorWindow : EditorWindow
    {
        private List<FeatureDefinition> features;
        private FeatureDefinition selectedFeature;
        private Vector2 leftScrollPos;
        private Vector2 rightScrollPos;
        private string searchString = "";
        private string newFeatureName = "";
        private bool isCreatingNew = false;

        [MenuItem("Tools/GameDevTools/Feature Aggregator")]
        public static void ShowWindow()
        {
            GetWindow<FeatureAggregatorWindow>("Feature Aggregator");
        }

        private void OnEnable()
        {
            RefreshFeatureList();
        }
        
        private void OnFocus()
        {
             RefreshFeatureList();
        }

        private void RefreshFeatureList()
        {
            features = FeatureManager.GetAllFeatures();
        }

        private void OnGUI()
        {
            // Toolbar
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Features", EditorStyles.boldLabel, GUILayout.Width(70));
            searchString = GUILayout.TextField(searchString, EditorStyles.toolbarSearchField, GUILayout.Width(200)); 
            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ New Feature", EditorStyles.toolbarButton))
            {
                isCreatingNew = true;
                selectedFeature = null;
                newFeatureName = "";
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                RefreshFeatureList();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // Left Panel - Feature List
            DrawFeatureList();

            // Divider
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

            // Right Panel - Selected Feature Details
            DrawFeatureDetails();

            GUILayout.EndHorizontal();
        }

        private void DrawFeatureList()
        {
            GUILayout.BeginVertical(GUILayout.Width(250));
            leftScrollPos = GUILayout.BeginScrollView(leftScrollPos);

            if (isCreatingNew)
            {
                DrawCreateNewInterface();
            }

            if (features == null) return;

            foreach (var feature in features)
            {
                if (!string.IsNullOrEmpty(searchString) && !feature.featureName.ToLower().Contains(searchString.ToLower()))
                    continue;

                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                if (selectedFeature == feature)
                {
                    style.normal.textColor = Color.cyan;
                }

                if (GUILayout.Button(feature.featureName, style, GUILayout.Height(30)))
                {
                    selectedFeature = feature;
                    isCreatingNew = false;
                    GUI.FocusControl(null);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawCreateNewInterface()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Create New Feature", EditorStyles.boldLabel);
            newFeatureName = EditorGUILayout.TextField("Name", newFeatureName);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                isCreatingNew = false;
            }
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(newFeatureName))
                {
                    selectedFeature = FeatureManager.CreateFeature(newFeatureName);
                    RefreshFeatureList();
                    isCreatingNew = false;
                    newFeatureName = "";
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawFeatureDetails()
        {
            GUILayout.BeginVertical();
            rightScrollPos = GUILayout.BeginScrollView(rightScrollPos);

            if (selectedFeature != null)
            {
                SerializedObject so = new SerializedObject(selectedFeature);
                so.Update();

                // Header
                GUILayout.Space(10);
                EditorGUILayout.LabelField(selectedFeature.featureName, EditorStyles.boldLabel);
                EditorGUILayout.ObjectField(selectedFeature, typeof(FeatureDefinition), false); // Debug link
                GUILayout.Space(5);

                // Description
                SerializedProperty descProp = so.FindProperty("description");
                EditorGUILayout.PropertyField(descProp);
                
                GUILayout.Space(10);

                // Actions
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Open All Scripts", GUILayout.Height(30)))
                {
                    ScriptOpener.OpenScriptsInIDE(selectedFeature.relatedScripts);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Scripts List
                EditorGUILayout.LabelField($"Scripts ({selectedFeature.relatedScripts.Count})", EditorStyles.boldLabel);
                
                SerializedProperty scriptsProp = so.FindProperty("relatedScripts");
                
                // Custom list drawing to allow "Open" buttons per script
                for (int i = 0; i < scriptsProp.arraySize; i++)
                {
                    SerializedProperty scriptRef = scriptsProp.GetArrayElementAtIndex(i);
                    MonoScript script = (MonoScript)scriptRef.objectReferenceValue;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(scriptRef, GUIContent.none);
                    if (script != null && GUILayout.Button("Open", GUILayout.Width(50)))
                    {
                        AssetDatabase.OpenAsset(script);
                    }
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        scriptsProp.DeleteArrayElementAtIndex(i);
                    }
                    GUILayout.EndHorizontal();
                }

                // Drag and drop area for scripts
                DropAreaGUI("Drop Scripts Here", (obj) => 
                {
                    if (obj is MonoScript script)
                    {
                         FeatureManager.AddScriptToFeature(selectedFeature, script);
                    }
                });
                
                GUILayout.Space(10);

                // Related Assets (Scenes/Prefabs)
                EditorGUILayout.LabelField($"Related Assets ({selectedFeature.relatedAssets.Count})", EditorStyles.boldLabel);
                 SerializedProperty assetsProp = so.FindProperty("relatedAssets");
                 for (int i = 0; i < assetsProp.arraySize; i++)
                {
                    SerializedProperty assetRef = assetsProp.GetArrayElementAtIndex(i);
                    Object asset = assetRef.objectReferenceValue;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(assetRef, GUIContent.none);
                    if (asset != null && GUILayout.Button("Open", GUILayout.Width(50)))
                    {
                        AssetDatabase.OpenAsset(asset);
                    }
                     if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        assetsProp.DeleteArrayElementAtIndex(i);
                    }
                    GUILayout.EndHorizontal();
                }
                 
                  // Drag and drop area for Assets
                DropAreaGUI("Drop Other Assets Here", (obj) => 
                {
                    if (!(obj is MonoScript))
                    {
                         FeatureManager.AddAssetToFeature(selectedFeature, obj);
                    }
                });

                if (GUILayout.Button("Delete Feature", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("Delete Feature", 
                        $"Are you sure you want to delete {selectedFeature.featureName}?", "Yes", "No"))
                    {
                        FeatureManager.DeleteFeature(selectedFeature);
                        selectedFeature = null;
                        RefreshFeatureList();
                    }
                }

                so.ApplyModifiedProperties();
            }
            else
            {
                GUILayout.Label("Select a feature to view details", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DropAreaGUI(string label, System.Action<Object> onDrop)
        {
            Event evt = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, label, EditorStyles.helpBox);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object dragged_object in DragAndDrop.objectReferences)
                        {
                            onDrop?.Invoke(dragged_object);
                        }
                    }
                    Event.current.Use();
                    break;
            }
        }
    }
}
