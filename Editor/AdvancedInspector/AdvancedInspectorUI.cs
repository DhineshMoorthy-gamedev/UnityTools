using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;
using UnityProductivityTools.CodeEditor;

namespace UnityProductivityTools.AdvancedInspector
{
    [System.Serializable]
    public class AdvancedInspectorUI
    {
        private Vector2 scrollPos;
        private string searchString = "";
        private GameObject lastSelected;
        private UnityEditor.Editor[] componentEditors;
        private List<bool> foldouts = new List<bool>();
        
        // Advanced Features State
        private Dictionary<int, string> scriptEditors = new Dictionary<int, string>();
        private Dictionary<int, bool> editingScripts = new Dictionary<int, bool>();
        private Dictionary<int, Vector2> scriptScrolls = new Dictionary<int, Vector2>();
        private Dictionary<int, string> scriptSearchStrings = new Dictionary<int, string>();
        private Dictionary<int, bool> scriptSearchActive = new Dictionary<int, bool>();
        private Dictionary<int, int> scriptSearchCurrentIndex = new Dictionary<int, int>();
        private Dictionary<int, List<int>> scriptSearchMatches = new Dictionary<int, List<int>>();

        private bool showSidebar = true;
        private Vector2 sidebarScroll;
        private float sidebarButtonSize = 25f;
        
        private string pendingFocusKey = null;
        private float targetScrollY = -1f;
        private string highlightKey = null;
        private float highlightTimer = 0f;
        private string componentToExpand = null;

        public void Initialize()
        {
            UpdateSelection();
        }

        public void OnDisable()
        {
            CleanupEditors();
        }

        public void UpdateSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != lastSelected)
            {
                lastSelected = selected;
                UpdateEditors();
            }
        }

        public void UpdateEditors()
        {
            CleanupEditors();

            if (lastSelected == null) return;

            Component[] components = lastSelected.GetComponents<Component>();
            componentEditors = new UnityEditor.Editor[components.Length];
            foldouts.Clear();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                componentEditors[i] = UnityEditor.Editor.CreateEditor(components[i]);
                
                if (componentToExpand != null && components[i].GetType().FullName == componentToExpand)
                {
                    foldouts.Add(true);
                }
                else
                {
                    foldouts.Add(true);
                }
            }
            componentToExpand = null;
            
            scriptEditors.Clear();
            editingScripts.Clear();
        }

        public void CleanupEditors()
        {
            if (componentEditors != null)
            {
                for (int i = 0; i < componentEditors.Length; i++)
                {
                    if (componentEditors[i] != null)
                    {
                        Object.DestroyImmediate(componentEditors[i]);
                    }
                }
            }
            componentEditors = null;
        }

        public void Draw(Rect position)
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
            }

            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            if (showSidebar)
            {
                DrawSidebar();
                EditorGUILayout.Space(5);
            }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            
            EditorGUILayout.BeginVertical();
            
            float offset = showSidebar ? 360 : 0;
            float currentWidth = position.width - offset;
            EditorGUIUtility.labelWidth = Mathf.Clamp(currentWidth * 0.4f, 80, 200);

            DrawSearchBar();
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawComponents(position);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

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
                    }
                    GUI.backgroundColor = oldColorResource;

                    GUIContent content = new GUIContent(label, label);
                    var style = new GUIStyle(EditorStyles.miniButton);
                    style.alignment = TextAnchor.MiddleLeft;
                    style.clipping = TextClipping.Clip;
                    style.padding.left = 5;

                    if (GUILayout.Button(content, style, GUILayout.MaxWidth(280), GUILayout.Height(sidebarButtonSize)))
                    {
                        FocusFavorite(propData);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            var pending = PlayModeTracker.GetPendingDeltas();
            if (pending.Count > 0)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Pending Play Mode Changes", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply All", EditorStyles.miniButton))
                {
                    foreach (var d in pending.ToList()) PlayModeTracker.ApplyDelta(d);
                }
                if (GUILayout.Button("Discard All", EditorStyles.miniButton))
                {
                    PlayModeTracker.ClearDeltas();
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
        }

        private void FocusComponent(string typeName)
        {
            searchString = "";
            pendingFocusKey = typeName;
            highlightKey = typeName;
            highlightTimer = 1.0f;

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

        private void DrawComponents(Rect windowPosition)
        {
            if (componentEditors == null) return;

            var propertyFavorites = InspectorFavorites.GetPropertyFavorites();
            bool hasAnyFav = propertyFavorites.Count > 0;

            if (hasAnyFav)
            {
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
                                }
                                
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
                
                EditorGUILayout.BeginHorizontal();
                if (pendingFocusKey == compType && Event.current.type == EventType.Repaint)
                {
                    targetScrollY = cRect.y;
                    pendingFocusKey = null;
                }

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
                
                float buttonGroupWidth = (comp is MonoBehaviour) ? 65 : 35;
                EditorGUILayout.BeginHorizontal(GUILayout.Width(buttonGroupWidth));
                
                if (GUILayout.Button("F", GUILayout.Width(25))) 
                {
                    ShowPropertyFavoriteMenu(comp);
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
                    DrawScriptEditor(i, comp, windowPosition);
                }

                if (foldouts[i])
                {
                    EditorGUIUtility.wideMode = (windowPosition.width - (showSidebar ? 150 : 0)) > 330;

                    EditorGUI.BeginChangeCheck();
                    
                    if (matchingProps != null && matchingProps.Count > 0)
                    {
                        componentEditors[i].serializedObject.Update();
                        foreach (var path in matchingProps)
                        {
                            SerializedProperty p = componentEditors[i].serializedObject.FindProperty(path);
                            if (p != null) EditorGUILayout.PropertyField(p, true);
                        }
                    }
                    else
                    {
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

        private void DrawScriptEditor(int index, Component comp, Rect windowPosition)
        {
            float offset = showSidebar ? 360 : 20;
            float availableWidth = windowPosition.width - offset - 30;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(availableWidth));
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Script Editor", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Maximize", EditorStyles.miniButton))
            {
                MonoScript script = MonoScript.FromMonoBehaviour(comp as MonoBehaviour);
                if (script != null) StandaloneCodeEditor.OpenScript(script);
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
    }
}
