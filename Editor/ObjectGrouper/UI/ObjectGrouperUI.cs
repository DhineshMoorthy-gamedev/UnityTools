using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityTools.ObjectGrouper.Core;
using UnityTools.ObjectGrouper.Data;

namespace UnityTools.ObjectGrouper.UI
{
    [System.Serializable]
    public class ObjectGrouperUI
    {
        private Vector2 _scrollPosition;
        private string _newGroupName = "New Group";

        public void Initialize()
        {
            // Initial setup if needed
        }

        public void Draw()
        {
            DrawToolbar();
            DrawGroupList();
            DrawDropArea();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label("Create:", EditorStyles.miniLabel, GUILayout.Width(45));
            _newGroupName = EditorGUILayout.TextField(_newGroupName, EditorStyles.toolbarTextField, GUILayout.Width(150));
            
            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                ObjectGroupManager.instance.CreateGroup(_newGroupName, Selection.gameObjects);
                _newGroupName = "New Group"; // reset
            }

            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Create from Selection", EditorStyles.toolbarButton))
            {
                ObjectGroupManager.instance.CreateGroup($"Group_{Selection.activeGameObject?.name ?? "Selection"}", Selection.gameObjects);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroupList()
        {
            var groups = ObjectGroupManager.instance.GetGroups();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No groups created. Create one or drag objects here.", MessageType.Info);
            }
            else
            {
                foreach (var group in groups)
                {
                    DrawGroupItem(group);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupItem(ObjectGroup group)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Visibility Toggle
            bool newVis = GUILayout.Toggle(group.IsVisible, "👁", EditorStyles.miniButton, GUILayout.Width(25));
            if (newVis != group.IsVisible)
            {
                ObjectGroupManager.instance.SetGroupVisibility(group, newVis);
            }

            // Lock Toggle
            bool newLock = GUILayout.Toggle(group.IsLocked, "🔒", EditorStyles.miniButton, GUILayout.Width(25));
            if (newLock != group.IsLocked)
            {
                ObjectGroupManager.instance.SetGroupLock(group, newLock);
            }

            // Color Picker
            group.GroupColor = EditorGUILayout.ColorField(group.GroupColor, GUILayout.Width(40));

            // Name
            group.Name = EditorGUILayout.TextField(group.Name);

            // Select Objects Button
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                var objs = ObjectGroupManager.instance.GetObjectsInGroup(group);
                if (objs.Count > 0)
                    Selection.objects = objs.ToArray();
            }


            // Add Selected to Group Button
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                ObjectGroupManager.instance.AddObjectsToGroup(group, Selection.gameObjects);
            }

            // Remove Selected from Group Button
            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                ObjectGroupManager.instance.RemoveObjectsFromGroup(group, Selection.gameObjects);
            }
            
            // Delete Group
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Group", $"Delete group '{group.Name}'? Objects will remain in scene.", "Yes", "No"))
                {
                    ObjectGroupManager.instance.DeleteGroup(group);
                    GUIUtility.ExitGUI(); // Prevent layout errors after modification
                }
            }

            EditorGUILayout.EndHorizontal();
            
            // Allow drag and drop onto group area to add objects
            Rect groupRect = GUILayoutUtility.GetLastRect();
            HandleDragDrop(groupRect, group);

            EditorGUILayout.EndVertical();
        }

        private void DrawDropArea()
        {
            GUILayout.FlexibleSpace();
            Rect dropRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag Objects Here to Create New Group", EditorStyles.helpBox);
            
            HandleDragDrop(dropRect, null); // Null group means create new
        }

        private void HandleDragDrop(Rect rect, ObjectGroup targetGroup)
        {
            Event evt = Event.current;
            if (rect.Contains(evt.mousePosition))
            {
                switch (evt.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            
                            var gos = new List<GameObject>();
                            foreach (var obj in DragAndDrop.objectReferences)
                            {
                                if (obj is GameObject go)
                                    gos.Add(go);
                            }

                            if (gos.Count > 0)
                            {
                                if (targetGroup != null)
                                {
                                    ObjectGroupManager.instance.AddObjectsToGroup(targetGroup, gos.ToArray());
                                }
                                else
                                {
                                    ObjectGroupManager.instance.CreateGroup($"Group_{gos[0].name}", gos.ToArray());
                                }
                            }
                        }
                        break;
                }
            }
        }
    }
}
