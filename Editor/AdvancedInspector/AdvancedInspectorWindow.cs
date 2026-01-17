using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityProductivityTools.AdvancedInspector
{
    public class AdvancedInspectorWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private string searchString = "";
        private GameObject lastSelected;
        private Editor[] componentEditors;
        private List<bool> foldouts = new List<bool>();

        [MenuItem("Tools/GameDevTools/Advanced Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<AdvancedInspectorWindow>("Advanced Inspector");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            CleanupEditors();
        }

        private void OnSelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != lastSelected)
            {
                lastSelected = selected;
                UpdateEditors();
                Repaint();
            }
        }

        private void UpdateEditors()
        {
            CleanupEditors();

            if (lastSelected == null) return;

            Component[] components = lastSelected.GetComponents<Component>();
            componentEditors = new Editor[components.Length];
            foldouts.Clear();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                componentEditors[i] = Editor.CreateEditor(components[i]);
                foldouts.Add(true);
            }
        }

        private void CleanupEditors()
        {
            if (componentEditors != null)
            {
                for (int i = 0; i < componentEditors.Length; i++)
                {
                    if (componentEditors[i] != null)
                    {
                        DestroyImmediate(componentEditors[i]);
                    }
                }
            }
            componentEditors = null;
        }

        private bool showSidebar = true;
        private Vector2 sidebarScroll;
        private float sidebarButtonSize = 25f;
        
        private string pendingFocusKey = null;
        private float targetScrollY = -1f;
        private string highlightKey = null;
        private float highlightTimer = 0f;

        private void OnGUI()
        {
            if (lastSelected == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject to inspect.", MessageType.Info);
                return;
            }

            // Handle delayed scroll
            if (targetScrollY >= 0)
            {
                scrollPos.y = targetScrollY;
                targetScrollY = -1f;
            }

            // Update highlight timer
            if (highlightTimer > 0)
            {
                highlightTimer -= 0.01f;
                Repaint();
            }

            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            if (showSidebar)
            {
                DrawSidebar();
                EditorGUILayout.Space(5);
            }

            // Record original label width to restore later
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            
            EditorGUILayout.BeginVertical();
            
            // Adjust label width for the remaining space
            float offset = showSidebar ? 360 : 0;
            float currentWidth = position.width - offset;
            EditorGUIUtility.labelWidth = Mathf.Clamp(currentWidth * 0.4f, 80, 200);

            DrawSearchBar();
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawComponents();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Restore label width
            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button(showSidebar ? "◀" : "▶", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                showSidebar = !showSidebar;
            }

            GUILayout.Label(lastSelected.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            bool isMultiSelect = Selection.gameObjects.Length > 1;
            EditorGUI.BeginDisabledGroup(!isMultiSelect);
            if (GUILayout.Button("Bulk Edit", EditorStyles.toolbarButton))
            {
                BulkComponentEditor.ShowWindow(Selection.gameObjects);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350), GUILayout.MinWidth(350), GUILayout.MaxWidth(400), GUILayout.ExpandHeight(true));
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, EditorStyles.helpBox);
            
            GUILayout.Label("Quick Access", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Favorited Properties
            var favProps = InspectorFavorites.GetPropertyFavorites();
            if (favProps.Count > 0)
            {
                GUILayout.Label("Properties", EditorStyles.miniBoldLabel);
                foreach (var propData in favProps)
                {
                    string compName = propData.compType.Split('.').Last();
                    string propName = propData.propPath.Split('.').Last();
                    string label = $"[{propData.objectName}] {compName}: {propName}";
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    Color oldColorResource = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(25), GUILayout.Height(sidebarButtonSize)))
                    {
                        InspectorFavorites.RemovePropertyFavoriteByData(propData);
                        Repaint();
                    }
                    GUI.backgroundColor = oldColorResource;

                    GUIContent content = new GUIContent(label, label);
                    var style = new GUIStyle(EditorStyles.miniButton);
                    style.alignment = TextAnchor.MiddleLeft;
                    style.clipping = TextClipping.Clip;
                    style.padding.left = 5;

                    // Strictly limit the name width
                    if (GUILayout.Button(content, style, GUILayout.MaxWidth(280), GUILayout.Height(sidebarButtonSize)))
                    {
                        FocusFavorite(propData);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void FocusFavorite(InspectorFavorites.FavoritePropertyData data)
        {
            searchString = "";
            string longKey = $"{data.guid}:{data.compType}:{data.propPath}";
            pendingFocusKey = longKey;
            highlightKey = longKey;
            highlightTimer = 1.5f;

            if (GlobalObjectId.TryParse(data.guid, out var gid))
            {
                Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj is GameObject go)
                {
                    if (Selection.activeGameObject != go)
                    {
                        Selection.activeGameObject = go;
                    }
                }
            }
            Repaint();
        }



        private void FocusComponent(string typeName)
        {
            searchString = "";
            pendingFocusKey = typeName;
            highlightKey = typeName;
            highlightTimer = 1f;
            Repaint();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal();
            searchString = EditorGUILayout.TextField("", searchString, "SearchTextField");
            if (GUILayout.Button("", "SearchCancelButton"))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComponents()
        {
            if (componentEditors == null) return;

            // Draw Favorites Section first
            var propertyFavorites = InspectorFavorites.GetPropertyFavorites();
            bool hasAnyFav = propertyFavorites.Count > 0;

            if (hasAnyFav)
            {
                // Property Favorites
                bool drawHeader = false;
                foreach (var propData in propertyFavorites)
                {
                    if (propData.guid == GlobalObjectId.GetGlobalObjectIdSlow(lastSelected).ToString())
                    {
                        if (!drawHeader)
                        {
                            GUILayout.Label("Favorites", EditorStyles.boldLabel);
                            drawHeader = true;
                        }

                        for (int i = 0; i < componentEditors.Length; i++)
                        {
                            if (componentEditors[i] == null) continue;
                            var comp = componentEditors[i].target as Component;
                            if (comp != null && comp.GetType().FullName == propData.compType)
                            {
                                Rect pRect = EditorGUILayout.BeginVertical();
                                string longKey = $"{propData.guid}:{propData.compType}:{propData.propPath}";
                                if (pendingFocusKey == longKey && Event.current.type == EventType.Repaint)
                                {
                                    targetScrollY = pRect.y;
                                    pendingFocusKey = null;
                                    Repaint();
                                }
                                
                                // Highlight
                                if (highlightKey == longKey && highlightTimer > 0)
                                {
                                    Color c = GUI.color;
                                    GUI.color = Color.Lerp(c, Color.cyan, highlightTimer);
                                    DrawFavoriteProperty(componentEditors[i].serializedObject, propData.propPath, comp.GetType().Name);
                                    GUI.color = c;
                                }
                                else
                                {
                                    DrawFavoriteProperty(componentEditors[i].serializedObject, propData.propPath, comp.GetType().Name);
                                }
                                
                                EditorGUILayout.EndVertical();
                            }
                        }
                    }
                }
                
                if (drawHeader)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                }
            }

            GUILayout.Label("Components", EditorStyles.boldLabel);

            for (int i = 0; i < componentEditors.Length; i++)
            {
                if (componentEditors[i] == null) continue;

                Component comp = componentEditors[i].target as Component;
                string compType = comp.GetType().FullName;
                string compName = comp.GetType().Name;

                bool componentMatches = MatchesSearch(compName, searchString);
                List<string> matchingProps = null;

                if (!componentMatches && !string.IsNullOrEmpty(searchString))
                {
                    matchingProps = GetMatchingPropertyPaths(componentEditors[i].serializedObject, searchString);
                    if (matchingProps.Count == 0) continue;
                }
                
                Rect cRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Track rect for focusing
                EditorGUILayout.BeginHorizontal();
                if (pendingFocusKey == compType && Event.current.type == EventType.Repaint)
                {
                    targetScrollY = cRect.y;
                    pendingFocusKey = null;
                    Repaint();
                }

                // Highlight background
                if (highlightKey == compType && highlightTimer > 0)
                {
                    Color oc = GUI.color;
                    GUI.color = Color.Lerp(oc, Color.cyan, highlightTimer);
                    foldouts[i] = EditorGUILayout.Foldout(foldouts[i], compName, true, EditorStyles.foldoutHeader);
                    GUI.color = oc;
                }
                else
                {
                    foldouts[i] = EditorGUILayout.Foldout(foldouts[i], compName, true, EditorStyles.foldoutHeader);
                }
                
                GUILayout.FlexibleSpace();
                
                // Use a horizontal block for buttons to prevent clipping
                EditorGUILayout.BeginHorizontal(GUILayout.Width(60));
                
                if (GUILayout.Button("F", GUILayout.Width(25))) 
                {
                    ShowPropertyFavoriteMenu(comp);
                }

                if (GUILayout.Button("P", GUILayout.Width(25)))
                {
                    ShowPresetMenu(comp);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndHorizontal();

                if (foldouts[i])
                {
                    // Ensure wideMode is set correctly for the current width
                    EditorGUIUtility.wideMode = (position.width - (showSidebar ? 150 : 0)) > 330;

                    EditorGUI.BeginChangeCheck();
                    
                    if (matchingProps != null && matchingProps.Count > 0)
                    {
                        // Smart Mode: Only draw matching properties
                        componentEditors[i].serializedObject.Update();
                        foreach (var path in matchingProps)
                        {
                            SerializedProperty p = componentEditors[i].serializedObject.FindProperty(path);
                            if (p != null) EditorGUILayout.PropertyField(p, true);
                        }
                    }
                    else
                    {
                        // Full Mode
                        componentEditors[i].OnInspectorGUI();
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        componentEditors[i].serializedObject.ApplyModifiedProperties();
                    }

                    DrawNullReferenceWarnings(componentEditors[i].serializedObject);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        private void ShowPropertyFavoriteMenu(Component component)
        {
            GenericMenu menu = new GenericMenu();
            SerializedObject so = new SerializedObject(component);
            SerializedProperty prop = so.GetIterator();

            string compType = component.GetType().FullName;
            GameObject sourceGo = component.gameObject;

            if (prop.NextVisible(true))
            {
                do
                {
                    string path = prop.propertyPath;
                    string name = prop.displayName;
                    bool isFav = InspectorFavorites.IsPropertyFavorite(sourceGo, compType, path);
                    
                    menu.AddItem(new GUIContent(name), isFav, () => {
                        if (isFav) InspectorFavorites.RemovePropertyFavorite(sourceGo, compType, path);
                        else InspectorFavorites.AddPropertyFavorite(sourceGo, compType, path);
                        Repaint();
                    });
                } while (prop.NextVisible(false));
            }

            menu.ShowAsContext();
        }

        private void DrawNullReferenceWarnings(SerializedObject so)
        {
            if (so == null) return;
            
            so.Update();
            SerializedProperty prop = so.GetIterator();
            List<string> nullProps = new List<string>();

            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && 
                        prop.objectReferenceValue == null && 
                        !prop.name.Equals("m_Script"))
                    {
                        nullProps.Add(prop.displayName);
                    }
                } while (prop.NextVisible(false));
            }

            if (nullProps.Count > 0)
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.HelpBox($"Null References: {string.Join(", ", nullProps)}", MessageType.Warning);
                GUI.backgroundColor = oldBg;
            }
        }

        private void ShowPresetMenu(Component component)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Save as Preset"), false, () => ComponentPresetManager.SavePreset(component));
            menu.AddSeparator("");

            string[] presets = ComponentPresetManager.GetPresetsForComponent(component.GetType().Name);
            if (presets.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No presets found"));
            }
            else
            {
                foreach (string presetPath in presets)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(presetPath);
                    menu.AddItem(new GUIContent($"Apply/{fileName}"), false, () => ComponentPresetManager.ApplyPreset(component, presetPath));
                }
            }

            menu.ShowAsContext();
        }

        private void DrawFavoriteProperty(SerializedObject so, string path, string compName)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{compName} > {prop.displayName}", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, true);
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private bool MatchesSearch(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(text)) return false;

            if (pattern.StartsWith("/"))
            {
                try
                {
                    string regexPattern = pattern.Substring(1);
                    return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return text.ToLower().Contains(pattern.ToLower());
        }

        private List<string> GetMatchingPropertyPaths(SerializedObject so, string pattern)
        {
            List<string> paths = new List<string>();
            SerializedProperty prop = so.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (MatchesSearch(prop.displayName, pattern) || MatchesSearch(prop.name, pattern))
                    {
                        paths.Add(prop.propertyPath);
                    }
                } while (prop.NextVisible(false));
            }
            return paths;
        }





    }
}
