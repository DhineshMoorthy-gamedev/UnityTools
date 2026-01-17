using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;

namespace UnityProductivityTools.AdvancedInspector
{
    public class AdvancedInspectorWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private string searchString = "";
        private GameObject lastSelected;
        private Editor[] componentEditors;
        private List<bool> foldouts = new List<bool>();
        
        // Advanced Features State
        private Dictionary<int, string> scriptEditors = new Dictionary<int, string>();
        private Dictionary<int, bool> editingScripts = new Dictionary<int, bool>();
        private Dictionary<int, Vector2> scriptScrolls = new Dictionary<int, Vector2>();
        private Dictionary<int, string> scriptSearchStrings = new Dictionary<int, string>();
        private Dictionary<int, bool> scriptSearchActive = new Dictionary<int, bool>();
        private Dictionary<int, int> scriptSearchCurrentIndex = new Dictionary<int, int>();
        private Dictionary<int, List<int>> scriptSearchMatches = new Dictionary<int, List<int>>();

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
                
                // If this is the component we want to expand/ping
                if (componentToExpand != null && components[i].GetType().FullName == componentToExpand)
                {
                    foldouts.Add(true);
                }
                else
                {
                    foldouts.Add(true); // Default to open for now, or match previous if possible
                }
            }
            componentToExpand = null;
            
            scriptEditors.Clear();
            editingScripts.Clear();
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
        private string componentToExpand = null;

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

            // Play Mode Deltas
            var pending = PlayModeTracker.GetPendingDeltas();
            if (pending.Count > 0)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Pending Play Mode Changes", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply All", EditorStyles.miniButton))
                {
                    foreach (var d in pending.ToList()) PlayModeTracker.ApplyDelta(d);
                    Repaint();
                }
                if (GUILayout.Button("Discard All", EditorStyles.miniButton))
                {
                    PlayModeTracker.ClearDeltas();
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();

                string keyToResolve = null;
                bool shouldApply = false;
                PlayModeTracker.PropertyDelta targetDelta = null;

                foreach (var delta in pending)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    string label = $"[{delta.displayName}] {delta.valueStr}";
                    
                    if (GUILayout.Button(new GUIContent(label, "Click to Ping in Inspector"), EditorStyles.miniLabel, GUILayout.MaxWidth(250)))
                    {
                        FocusPendingChange(delta);
                    }
                    
                    if (GUILayout.Button("✓", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        keyToResolve = delta.Key;
                        targetDelta = delta;
                        shouldApply = true;
                    }
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        keyToResolve = delta.Key;
                        shouldApply = false;
                    }
                    EditorGUILayout.EndHorizontal();
                    if (keyToResolve != null) break;
                }

                if (keyToResolve != null)
                {
                    if (shouldApply && targetDelta != null) PlayModeTracker.ApplyDelta(targetDelta);
                    else PlayModeTracker.DiscardDelta(keyToResolve);
                    Repaint();
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
            highlightTimer = 1.0f;

            // Expand it
            if (componentEditors != null)
            {
                for (int i = 0; i < componentEditors.Length; i++)
                {
                    if (componentEditors[i] != null && componentEditors[i].target.GetType().FullName == typeName)
                    {
                        foldouts[i] = true;
                        break;
                    }
                }
            }
            Repaint();
        }

        private void FocusPendingChange(PlayModeTracker.PropertyDelta delta)
        {
            GameObject go = GameObject.Find(delta.scenePath);
            if (go == null)
            {
                Debug.LogWarning($"[AdvancedInspector] Cannot ping: Object at '{delta.scenePath}' not found.");
                return;
            }

            componentToExpand = delta.compType;
            pendingFocusKey = delta.compType;
            highlightKey = delta.compType;
            highlightTimer = 1.5f;

            if (Selection.activeGameObject != go)
            {
                Selection.activeGameObject = go;
            }
            else
            {
                // Already selected, just trigger focus directly
                FocusComponent(delta.compType);
            }
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
                float buttonGroupWidth = (comp is MonoBehaviour) ? 90 : 60;
                EditorGUILayout.BeginHorizontal(GUILayout.Width(buttonGroupWidth));
                
                if (GUILayout.Button("F", GUILayout.Width(25))) 
                {
                    ShowPropertyFavoriteMenu(comp);
                }

                if (GUILayout.Button("P", GUILayout.Width(25)))
                {
                    ShowPresetMenu(comp);
                }

                if (comp is MonoBehaviour)
                {
                    if (GUILayout.Button("C", GUILayout.Width(25)))
                    {
                        ToggleScriptEditor(i, comp);
                    }
                }

                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndHorizontal();

                if (editingScripts.ContainsKey(i) && editingScripts[i])
                {
                    DrawScriptEditor(i, comp);
                }

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

        // --- Advanced Features Implementation ---

        private void ToggleScriptEditor(int index, Component comp)
        {
            if (editingScripts.ContainsKey(index))
            {
                editingScripts[index] = !editingScripts[index];
            }
            else
            {
                editingScripts[index] = true;
                MonoScript script = MonoScript.FromMonoBehaviour(comp as MonoBehaviour);
                if (script != null)
                {
                    scriptEditors[index] = script.text;
                }
            }
        }

        private void DrawScriptEditor(int index, Component comp)
        {
            float offset = showSidebar ? 360 : 20;
            float availableWidth = position.width - offset - 30;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(availableWidth));
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Script Editor", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Maximize", EditorStyles.miniButton))
            {
                ScriptEditorWindow.ShowWindow(comp, scriptEditors[index]);
            }
            EditorGUILayout.EndHorizontal();

            if (!scriptScrolls.ContainsKey(index)) scriptScrolls[index] = Vector2.zero;
            scriptScrolls[index] = EditorGUILayout.BeginScrollView(scriptScrolls[index], GUILayout.MinHeight(150), GUILayout.MaxHeight(400));
            
            GUIStyle areaStyle = new GUIStyle(EditorStyles.textArea);
            areaStyle.wordWrap = true;
            
            EditorGUI.BeginChangeCheck();
            string newText = EditorGUILayout.TextArea(scriptEditors[index], areaStyle, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                scriptEditors[index] = newText;
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & Compile", GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            {
                MonoScript script = MonoScript.FromMonoBehaviour(comp as MonoBehaviour);
                if (script != null)
                {
                    File.WriteAllText(AssetDatabase.GetAssetPath(script), scriptEditors[index]);
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(script));
                    editingScripts[index] = false;
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            {
                editingScripts[index] = false;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ToggleSearch(int index)
        {
            if (!scriptSearchActive.ContainsKey(index)) scriptSearchActive[index] = false;
            scriptSearchActive[index] = !scriptSearchActive[index];
            if (scriptSearchActive[index])
            {
                if (!scriptSearchStrings.ContainsKey(index)) scriptSearchStrings[index] = "";
            }
        }

        private void DrawScriptSearchBar(int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            EditorGUI.BeginChangeCheck();
            scriptSearchStrings[index] = EditorGUILayout.TextField(scriptSearchStrings[index], EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSearchMatches(index);
            }
            
            if (!string.IsNullOrEmpty(scriptSearchStrings[index]) && scriptSearchMatches.ContainsKey(index) && scriptSearchMatches[index].Count > 0)
            {
                if (!scriptSearchCurrentIndex.ContainsKey(index)) scriptSearchCurrentIndex[index] = 0;
                
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(25)))
                {
                    NavigateSearchPrevious(index);
                }
                
                GUILayout.Label($"{scriptSearchCurrentIndex[index] + 1}/{scriptSearchMatches[index].Count}", EditorStyles.miniLabel, GUILayout.Width(50));
                
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(25)))
                {
                    NavigateSearchNext(index);
                }
            }
            
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                scriptSearchActive[index] = false;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateSearchMatches(int index)
        {
            if (!scriptSearchMatches.ContainsKey(index)) scriptSearchMatches[index] = new List<int>();
            scriptSearchMatches[index].Clear();
            scriptSearchCurrentIndex[index] = 0;

            if (string.IsNullOrEmpty(scriptSearchStrings[index])) return;

            string text = scriptEditors[index];
            string search = scriptSearchStrings[index];
            int pos = 0;
            
            while ((pos = text.IndexOf(search, pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                scriptSearchMatches[index].Add(pos);
                pos += search.Length;
            }
        }

        private void NavigateSearchNext(int index)
        {
            if (!scriptSearchMatches.ContainsKey(index) || scriptSearchMatches[index].Count == 0) return;
            scriptSearchCurrentIndex[index] = (scriptSearchCurrentIndex[index] + 1) % scriptSearchMatches[index].Count;
            ScrollToMatch(index);
            Repaint();
        }

        private void NavigateSearchPrevious(int index)
        {
            if (!scriptSearchMatches.ContainsKey(index) || scriptSearchMatches[index].Count == 0) return;
            scriptSearchCurrentIndex[index]--;
            if (scriptSearchCurrentIndex[index] < 0) scriptSearchCurrentIndex[index] = scriptSearchMatches[index].Count - 1;
            ScrollToMatch(index);
            Repaint();
        }

        private void ScrollToMatch(int index)
        {
            if (!scriptSearchMatches.ContainsKey(index) || scriptSearchMatches[index].Count == 0) return;
            if (!scriptSearchCurrentIndex.ContainsKey(index)) return;

            int currentMatch = scriptSearchCurrentIndex[index];
            if (currentMatch >= scriptSearchMatches[index].Count) return;

            int matchPos = scriptSearchMatches[index][currentMatch];
            string text = scriptEditors[index];
            
            // Count lines before the match
            int lineNumber = 0;
            for (int i = 0; i < matchPos && i < text.Length; i++)
            {
                if (text[i] == '\n') lineNumber++;
            }

            // Estimate scroll position (approximate line height of 16 pixels)
            float lineHeight = 16f;
            float targetY = lineNumber * lineHeight;
            
            // Center the match in the view (scroll view height is approximately 150-400)
            targetY = Mathf.Max(0, targetY - 75);
            
            if (!scriptScrolls.ContainsKey(index)) scriptScrolls[index] = Vector2.zero;
            scriptScrolls[index] = new Vector2(scriptScrolls[index].x, targetY);
        }

        private string HighlightSearchMatches(string text, int index)
        {
            if (!scriptSearchActive.ContainsKey(index) || !scriptSearchActive[index]) return text;
            if (string.IsNullOrEmpty(scriptSearchStrings[index])) return text;
            if (!scriptSearchMatches.ContainsKey(index) || scriptSearchMatches[index].Count == 0) return text;

            // For TextArea, we can't use rich text, so we'll use a different approach
            // We'll show the highlighted version in a separate label above the text area
            return text;
        }





    }
}
