using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDevTools.Editor
{
    public class NoteDashboardWindow : EditorWindow
    {
        [MenuItem("Tools/GameDevTools/Note Dashboard")]
        public static void ShowWindow()
        {
            var window = GetWindow<NoteDashboardWindow>("Note Dashboard");
            window.minSize = new Vector2(300, 400);
        }

        private List<Note> _allNotes = new List<Note>();
        private string _searchString = "";
        private Vector2 _scrollPos;

        private void OnEnable()
        {
            RefreshNotes();
        }

        private void OnFocus()
        {
            RefreshNotes();
        }

        private void OnHierarchyChange()
        {
            RefreshNotes();
        }

        private void RefreshNotes()
        {
            _allNotes = FindObjectsOfType<Note>().ToList();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawNoteList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Search
            // Search
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField");
            if (searchStyle == null) searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField");
            if (searchStyle == null) searchStyle = EditorStyles.toolbarTextField;

            GUIStyle cancelStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton");
            if (cancelStyle == null) cancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton");
            if (cancelStyle == null) cancelStyle = GUI.skin.FindStyle("ToolbarCancelButton"); // Fallback
            
            string newSearch = GUILayout.TextField(_searchString, searchStyle, GUILayout.Width(200));
            if (newSearch != _searchString)
            {
                _searchString = newSearch;
            }
            
            if (cancelStyle != null && GUILayout.Button("", cancelStyle))
            {
                _searchString = "";
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                RefreshNotes();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoteList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_allNotes.Count == 0)
            {
                EditorGUILayout.HelpBox("No Notes found in the current scene.", MessageType.Info);
            }
            else
            {
                foreach (var note in _allNotes)
                {
                    if (note == null) continue;

                    // Filter
                    if (!string.IsNullOrEmpty(_searchString))
                    {
                        bool matchTitle = note.title.ToLower().Contains(_searchString.ToLower());
                        bool matchContent = note.content.ToLower().Contains(_searchString.ToLower());
                        if (!matchTitle && !matchContent) continue;
                    }

                    DrawNoteItem(note);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNoteItem(Note note)
        {
            // Start Vertical Box
            Rect rowRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Check for click on the entire row
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                Selection.activeGameObject = note.gameObject;
                EditorGUIUtility.PingObject(note.gameObject);
                SceneView.FrameLastActiveSceneView();
                Event.current.Use(); // Consume event
                Repaint(); // Force update
            }

            EditorGUILayout.BeginHorizontal();
            
            // Icon
            Color originalColor = GUI.color;
            GUI.color = note.gizmoColor;
            GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow"), GUILayout.Width(20), GUILayout.Height(20));
            GUI.color = originalColor;

            // Title
            GUILayout.Label(note.title, EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();

            // Preview Content
            string preview = note.content;
            if (!string.IsNullOrEmpty(preview))
            {
                if (preview.Length > 100) preview = preview.Substring(0, 100) + "...";
                EditorGUILayout.LabelField(preview, EditorStyles.wordWrappedMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}
