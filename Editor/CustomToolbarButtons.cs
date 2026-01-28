using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dhinesh.EditorTools.CustomTaskbar
{
    [InitializeOnLoad]
    public static class CustomToolbarButtons
    {
        static CustomToolbarButtons()
        {
            ToolbarExtender.LeftToolbarGUI.Clear();
            // ToolbarExtender.LeftToolbarGUI.Add(DrawSelectionBackButton);
            // ToolbarExtender.LeftToolbarGUI.Add(DrawSelectionForwardButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.LeftToolbarGUI.Add(DrawProjectSettingsButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.LeftToolbarGUI.Add(DrawPrefsButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.LeftToolbarGUI.Add(DrawTestButton);

            ToolbarExtender.RightToolbarGUI.Clear();
            // IMPORTANT: Static buttons first to keep them anchored left
            ToolbarExtender.RightToolbarGUI.Add(DrawReloadButton);
            ToolbarExtender.RightToolbarGUI.Add(DrawFindSceneButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.RightToolbarGUI.Add(DrawSelectionBackButton);
            ToolbarExtender.RightToolbarGUI.Add(DrawSelectionForwardButton);
            // Pinned items added to the same list so they follow the static buttons
            ToolbarExtender.RightToolbarGUI.Add(DrawPinnedItems);
        }

        static void DrawSelectionBackButton()
        {
            GUI.enabled = SelectionHistoryManager.CanGoBack;
            if (GUILayout.Button(new GUIContent("◀", "Previous Selection"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                SelectionHistoryManager.GoBack();
            }
            GUI.enabled = true;
        }

        static void DrawSelectionForwardButton()
        {
            GUI.enabled = SelectionHistoryManager.CanGoForward;
            if (GUILayout.Button(new GUIContent("▶", "Next Selection"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                SelectionHistoryManager.GoForward();
            }
            GUI.enabled = true;
        }

        static void DrawProjectSettingsButton()
        {
            if (GUILayout.Button(new GUIContent("⚙", "Open Project Settings"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                SettingsService.OpenProjectSettings("");
            }
        }

        static void DrawPrefsButton()
        {
            if (GUILayout.Button(new GUIContent("⚙", "Open Preferences"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                SettingsService.OpenUserPreferences("");
            }
        }

        static void DrawTestButton()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Switch Platform"), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();
                var currentTarget = EditorUserBuildSettings.activeBuildTarget;
                menu.AddItem(new GUIContent("Windows"), currentTarget == BuildTarget.StandaloneWindows64, () => SwitchPlatform(BuildTarget.StandaloneWindows64));
                menu.AddItem(new GUIContent("Android"), currentTarget == BuildTarget.Android, () => SwitchPlatform(BuildTarget.Android));
                menu.AddItem(new GUIContent("iOS"), currentTarget == BuildTarget.iOS, () => SwitchPlatform(BuildTarget.iOS));
                menu.AddItem(new GUIContent("WebGL"), currentTarget == BuildTarget.WebGL, () => SwitchPlatform(BuildTarget.WebGL));
                menu.ShowAsContext();
            }
        }

        static void SwitchPlatform(BuildTarget target)
        {
            if (EditorUtility.DisplayDialog("Switch Platform", $"Switch to {target}?", "Switch", "Cancel"))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            }
        }

        static void DrawReloadButton()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Switch Scene"), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();
                var scenes = EditorBuildSettings.scenes;
                foreach (var scene in scenes)
                {
                    if (!scene.enabled) continue;
                    var path = scene.path;
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    menu.AddItem(new GUIContent(name), UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == path, () => {
                        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
                    });
                }
                menu.ShowAsContext();
            }
        }

        static void DrawFindSceneButton()
        {
            if (GUILayout.Button(new GUIContent("🔍", "Find Scene"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                var path = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                if (!string.IsNullOrEmpty(path)) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            }
        }

        static void DrawPinnedItems()
        {
            var items = ToolbarPinManager.PinnedItems.ToList();
            if (items.Count == 0) return;

            GUILayout.Space(10); // Fixed gap after static tools

            foreach (var item in items)
            {
                var obj = ToolbarPinManager.Resolve(item.guid);
                var content = new GUIContent { image = obj != null ? AssetPreview.GetMiniThumbnail(obj) : null };
                content.tooltip = obj != null ? $"Pinned: {item.originalName}" : "Missing Object";
                
                if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    if (Event.current.button == 1) ToolbarPinManager.Unpin(item.guid);
                    else if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class ToolbarExtender
    {
        public static readonly List<Action> LeftToolbarGUI = new();
        public static readonly List<Action> RightToolbarGUI = new();

        static Type toolbarType;
        static ScriptableObject toolbarInstance;
        static FieldInfo rootField;

        static ToolbarExtender()
        {
            toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            EditorApplication.update += TryInit;
        }

        static void TryInit()
        {
            if (toolbarInstance != null) return;
            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0) return;

            toolbarInstance = (ScriptableObject)toolbars[0];
            rootField = toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            var root = rootField?.GetValue(toolbarInstance) as VisualElement;
            if (root == null) return;

            var leftZone = root.Q("ToolbarZoneLeft");
            var rightZone = root.Q("ToolbarZoneRight");
            var playZone = root.Q("ToolbarZonePlayMode");

            // Build unified containers
            var leftC = new IMGUIContainer(() => {
                GUILayout.BeginHorizontal();
                foreach (var gui in LeftToolbarGUI) gui?.Invoke();
                GUILayout.EndHorizontal();
            }) { style = { flexGrow = 0, flexDirection = FlexDirection.Row } };

            var rightC = new IMGUIContainer(() => {
                GUILayout.BeginHorizontal();
                foreach (var gui in RightToolbarGUI) gui?.Invoke();
                GUILayout.EndHorizontal();
            }) { style = { flexGrow = 0, flexDirection = FlexDirection.Row } };

            // Inject into zones
            if (leftZone != null) leftZone.Add(leftC);
            else if (playZone != null) playZone.Insert(0, leftC);

            if (rightZone != null) {
                // By putting everything in ONE container (rightC) and inserting it at index 0,
                // we ensure the static tools are always at the leftmost position of the cluster.
                rightZone.Insert(0, rightC);
            } else if (playZone != null) {
                playZone.Add(rightC);
            }

            EditorApplication.update -= TryInit;
        }

        public static void Repaint()
        {
            if (toolbarInstance != null)
                toolbarType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(toolbarInstance, null);
        }
    }
}
