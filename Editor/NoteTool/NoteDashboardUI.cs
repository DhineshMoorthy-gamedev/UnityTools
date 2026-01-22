using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDevTools.Editor
{
    [System.Serializable]
    public class NoteDashboardUI
    {
        private List<Note> _allNotes = new List<Note>();
        private string _searchString = "";
        private Vector2 _scrollPos;

        public void Initialize()
        {
            RefreshNotes();
        }

        public void OnFocus()
        {
            RefreshNotes();
        }

        public void RefreshNotes()
        {
            _allNotes = Object.FindObjectsOfType<Note>().ToList();
        }

        public void Draw()
        {
            DrawToolbar();
            DrawNoteList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? 
                                   GUI.skin.FindStyle("ToolbarSearchTextField") ?? 
                                   EditorStyles.toolbarTextField;

            GUIStyle cancelStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? 
                                    GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? 
                                    GUI.skin.FindStyle("ToolbarCancelButton");
            
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

            if (_allNotes.Count == 0 || _allNotes.All(n => n == null))
            {
                EditorGUILayout.HelpBox("No Notes found in the current scene.", MessageType.Info);
            }
            else
            {
                foreach (var note in _allNotes)
                {
                    if (note == null) continue;

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
            Rect rowRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                Selection.activeGameObject = note.gameObject;
                EditorGUIUtility.PingObject(note.gameObject);
                SceneView.FrameLastActiveSceneView();
                Event.current.Use();
            }

            EditorGUILayout.BeginHorizontal();
            
            Color originalColor = GUI.color;
            GUI.color = note.gizmoColor;
            GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow"), GUILayout.Width(20), GUILayout.Height(20));
            GUI.color = originalColor;

            GUILayout.Label(note.title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

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
