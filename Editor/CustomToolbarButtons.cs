using System;
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
            ToolbarExtender.LeftToolbarGUI.Add(DrawSelectionBackButton);
            ToolbarExtender.LeftToolbarGUI.Add(DrawSelectionForwardButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.LeftToolbarGUI.Add(DrawProjectSettingsButton);
            ToolbarExtender.LeftToolbarGUI.Add(() => GUILayout.Space(10));
            ToolbarExtender.LeftToolbarGUI.Add(DrawPrefsButton);
            ToolbarExtender.LeftToolbarGUI.Add(DrawTestButton);
            ToolbarExtender.RightToolbarGUI.Add(DrawReloadButton);
            ToolbarExtender.RightToolbarGUI.Add(DrawFindSceneButton);
        }
        static void DrawSelectionBackButton()
        {
            GUI.enabled = SelectionHistoryManager.CanGoBack;
            if (GUILayout.Button(
                new GUIContent("◀", "Previous Selection"),
                EditorStyles.toolbarButton,
                GUILayout.Width(28)))
            {
                SelectionHistoryManager.GoBack();
            }
            GUI.enabled = true;
        }

        static void DrawSelectionForwardButton()
        {
            GUI.enabled = SelectionHistoryManager.CanGoForward;
            if (GUILayout.Button(
                new GUIContent("▶", "Next Selection"),
                EditorStyles.toolbarButton,
                GUILayout.Width(28)))
            {
                SelectionHistoryManager.GoForward();
            }
            GUI.enabled = true;
        }

        static void DrawProjectSettingsButton()
        {
            if (GUILayout.Button(
                new GUIContent("⚙", "Open Project Settings"),
                EditorStyles.toolbarButton,
                GUILayout.Width(28)))
            {
                SettingsService.OpenProjectSettings("");
            }
        }

        static void DrawPrefsButton()
        {
            if (GUILayout.Button(
                new GUIContent("⚙", "Open Preferences"),
                EditorStyles.toolbarButton,
                GUILayout.Width(28)))
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

                menu.AddItem(new GUIContent("Windows"), currentTarget == BuildTarget.StandaloneWindows64, () =>
                {
                    if (EditorUtility.DisplayDialog("Switch Platform",
                        "Are you sure you want to switch to Windows platform?\n\nThis may take a few moments.",
                        "Switch", "Cancel"))
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(
                            BuildPipeline.GetBuildTargetGroup(BuildTarget.StandaloneWindows64),
                            BuildTarget.StandaloneWindows64
                        );
                        Debug.Log("Switched to Windows platform.");
                    }
                });

                menu.AddItem(new GUIContent("Android"), currentTarget == BuildTarget.Android, () =>
                {
                    if (EditorUtility.DisplayDialog("Switch Platform",
                        "Are you sure you want to switch to Android platform?\n\nThis may take a few moments.",
                        "Switch", "Cancel"))
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(
                            BuildPipeline.GetBuildTargetGroup(BuildTarget.Android),
                            BuildTarget.Android
                        );
                        Debug.Log("Switched to Android platform.");
                    }
                });

                menu.AddItem(new GUIContent("iOS"), currentTarget == BuildTarget.iOS, () =>
                {
                    if (EditorUtility.DisplayDialog("Switch Platform",
                        "Are you sure you want to switch to iOS platform?\n\nThis may take a few moments.",
                        "Switch", "Cancel"))
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(
                            BuildPipeline.GetBuildTargetGroup(BuildTarget.iOS),
                            BuildTarget.iOS
                        );
                        Debug.Log("Switched to iOS platform.");
                    }
                });

                menu.AddItem(new GUIContent("WebGL"), currentTarget == BuildTarget.WebGL, () =>
                {
                    if (EditorUtility.DisplayDialog("Switch Platform",
                        "Are you sure you want to switch to WebGL platform?\n\nThis may take a few moments.",
                        "Switch", "Cancel"))
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(
                            BuildPipeline.GetBuildTargetGroup(BuildTarget.WebGL),
                            BuildTarget.WebGL
                        );
                        Debug.Log("Switched to WebGL platform.");
                    }
                });

                menu.ShowAsContext();
            }
            GUILayout.Space(4);
        }

        static void DrawReloadButton()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Switch Scene"), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();

                // Get all scenes from build settings
                var scenes = EditorBuildSettings.scenes;
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

                if (scenes.Length == 0)
                {
                    menu.AddDisabledItem(new GUIContent("No scenes in build settings"));
                }
                else
                {
                    foreach (var scene in scenes)
                    {
                        if (!scene.enabled) continue; // Skip disabled scenes

                        var scenePath = scene.path;
                        var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                        bool isCurrentScene = scenePath == currentScene;

                        menu.AddItem(new GUIContent(sceneName), isCurrentScene, () =>
                        {
                            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                                Debug.Log($"Opened scene: {sceneName}");
                            }
                        });
                    }
                }

                menu.ShowAsContext();
            }
        }

        static void DrawFindSceneButton()
        {
            if (GUILayout.Button(
                new GUIContent("🔍", "Find active scene in Project"),
                EditorStyles.toolbarButton,
                GUILayout.Width(28)))
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogWarning("Active scene has no asset path.");
                    return;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScene.path);
                if (sceneAsset != null)
                {
                    Selection.activeObject = sceneAsset;
                    EditorGUIUtility.PingObject(sceneAsset);
                }
            }
        }

    }



    /// <summary>
    /// Safe toolbar extender (null-protected)
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarExtender
    {
        public static readonly System.Collections.Generic.List<Action> LeftToolbarGUI = new();
        public static readonly System.Collections.Generic.List<Action> RightToolbarGUI = new();

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
            if (toolbars == null || toolbars.Length == 0)
                return;

            toolbarInstance = (ScriptableObject)toolbars[0];
            rootField = toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rootField == null) return;

            var root = rootField.GetValue(toolbarInstance) as VisualElement;
            if (root == null) return;

            // Find the play mode buttons container
            var playModeButtons = root.Q("ToolbarZonePlayMode");
            if (playModeButtons == null) return;

            // Create container for Test button (left of play button)
            var leftContainer = new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal();
                foreach (var gui in LeftToolbarGUI)
                    gui?.Invoke();
                GUILayout.EndHorizontal();
            })
            {
                style =
                {
                    flexGrow = 0,
                    flexDirection = FlexDirection.Row
                }
            };

            // Insert Test button before the play button (at index 0)
            playModeButtons.Insert(0, leftContainer);

            // Create container for Reload button (right of step button)
            var rightContainer = new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal();
                foreach (var gui in RightToolbarGUI)
                    gui?.Invoke();
                GUILayout.EndHorizontal();
            })
            {
                style =
                {
                    flexGrow = 0,
                    flexDirection = FlexDirection.Row
                }
            };

            // Add Reload button after all existing buttons (after step button)
            playModeButtons.Add(rightContainer);

            EditorApplication.update -= TryInit;
        }
    }
}
