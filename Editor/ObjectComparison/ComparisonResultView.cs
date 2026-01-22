using UnityEngine;
using UnityEditor;

namespace UnityTools.ObjectComparison
{
    public class ComparisonResultView
    {
        private bool drawingTransformExpanded = true;
        private bool drawingHierarchyExpanded = true;
        private bool drawingGameObjectExpanded = true;
        private System.Collections.Generic.HashSet<string> drawingCollapsedComponents = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.HashSet<string> drawingSyncedPaths = new System.Collections.Generic.HashSet<string>();

        private bool isTransformExpanded = true;
        public bool ShowOnlyDifferences = false;
        private bool drawingShowOnlyDifferences = false;

        public void Draw(ComparisonResult result)
        {
            if (result == null || result.ObjectDiff == null) return;

            PrepareFrame();

            DrawObjectHeader(result.ObjectDiff);
            DrawTransformComparison(result.ObjectDiff);
            DrawComponentComparison(result);
            
            // Only draw hierarchy if not filtering or if there are diffs?
            // Usually hierarchy is big.
             bool hasHierarchyDiffs = false;
             foreach(var c in result.ObjectDiff.ChildDifferences) if(c.Status != DiffType.Identical) hasHierarchyDiffs = true;

            if (!drawingShowOnlyDifferences || hasHierarchyDiffs)
                DrawHierarchyComparison(result.ObjectDiff);

            GUILayout.FlexibleSpace();
        }

        private void PrepareFrame()
        {
            if (Event.current.type == EventType.Layout)
            {
                drawingTransformExpanded = isTransformExpanded;
                drawingHierarchyExpanded = isHierarchyExpanded;
                drawingGameObjectExpanded = isGameObjectExpanded;
                
                drawingCollapsedComponents.Clear();
                foreach (var key in collapsedComponents) drawingCollapsedComponents.Add(key);

                drawingSyncedPaths.Clear();
                foreach (var path in SyncedPaths) drawingSyncedPaths.Add(path);

                drawingShowOnlyDifferences = ShowOnlyDifferences;
            }
        }

        private bool isHierarchyExpanded = true;

        private void DrawHierarchyComparison(ObjectDiff diff)
        {
            if (diff.ChildDifferences == null || diff.ChildDifferences.Count == 0) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            bool wasExpanded = drawingHierarchyExpanded;
            bool nowExpanded = EditorGUILayout.Foldout(wasExpanded, $"Children ({diff.ChildDifferences.Count})", true);
            if (nowExpanded != wasExpanded) isHierarchyExpanded = nowExpanded;
            
            EditorGUILayout.EndHorizontal();

            if (wasExpanded)
            {
                EditorGUILayout.BeginVertical("box");
                foreach (var child in diff.ChildDifferences)
                {
                    DrawChildRow(child);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
        }

        private void DrawChildRow(ChildDiff child)
        {
            Color color = ComparisonStyles.GetColorForDiff(child.Status);
            if(child.Status == DiffType.Identical) color = Color.clear;

            GUI.backgroundColor = (color == Color.clear) ? Color.white : color;
            EditorGUILayout.BeginHorizontal(ComparisonStyles.RowBackground);
            GUI.backgroundColor = Color.white;
            
            string icon = "";
            switch(child.Status) {
                case DiffType.Identical: icon = "✓"; break;
                case DiffType.Missing: icon = "✗"; break;
                case DiffType.Added: icon = "⊕"; break;
            }

            Color textColor = (child.Status == DiffType.Identical) ? ComparisonStyles.TextGreen : 
                              (child.Status == DiffType.Missing) ? ComparisonStyles.TextRed : ComparisonStyles.TextBlue;

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = textColor;
            GUILayout.Label(icon, style, GUILayout.Width(20));
            
            EditorGUILayout.LabelField(child.Name);
            
            if(child.Status == DiffType.Missing)
                GUILayout.Label("(Missing in B)", EditorStyles.miniLabel);
            else if(child.Status == DiffType.Added)
                GUILayout.Label("(Added in B)", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private bool isGameObjectExpanded = true;
        public System.Collections.Generic.HashSet<string> SyncedPaths = new System.Collections.Generic.HashSet<string>();

        private void DrawObjectHeader(ObjectDiff diff)
        {
            bool hasDiffs = diff.NameDiff != DiffType.Identical || diff.TagDiff != DiffType.Identical || 
                            diff.LayerDiff != DiffType.Identical || diff.ActiveDiff != DiffType.Identical;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            bool wasExpanded = drawingGameObjectExpanded;
            bool nowExpanded = EditorGUILayout.Foldout(wasExpanded, "GameObject Properties", true);
            if (nowExpanded != wasExpanded) isGameObjectExpanded = nowExpanded;
            
            var statusIcon = !hasDiffs ? "✓" : "⚠";
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = !hasDiffs ? ComparisonStyles.TextGreen : ComparisonStyles.TextOrange;
            GUILayout.Label(statusIcon, style, GUILayout.Width(20));
            
            EditorGUILayout.EndHorizontal();

            if (wasExpanded)
            {
                EditorGUILayout.BeginVertical("box", GUILayout.MaxWidth(600), GUILayout.ExpandHeight(false));
                
                DrawRow("Name", diff.ObjectA.name, diff.ObjectB.name, diff.NameDiff, null, null, null, "GameObject Icon");
                DrawRow("Tag", diff.ObjectA.tag, diff.ObjectB.tag, diff.TagDiff, null, null, null, "d_FilterByLabel");
                DrawRow("Layer", LayerMask.LayerToName(diff.ObjectA.layer), LayerMask.LayerToName(diff.ObjectB.layer), diff.LayerDiff, null, null, null, "d_FilterByLabel");
                DrawRow("Active", diff.ObjectA.activeSelf.ToString(), diff.ObjectB.activeSelf.ToString(), diff.ActiveDiff, null, null, null, diff.ObjectA.activeSelf ? "d_VisibilityOn" : "d_VisibilityOff");
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
        }

        private void DrawTransformComparison(ObjectDiff diff)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            bool wasExpanded = drawingTransformExpanded;
            bool nowExpanded = EditorGUILayout.Foldout(wasExpanded, "Transform", true);
            if (nowExpanded != wasExpanded) isTransformExpanded = nowExpanded;
            
            var statusIcon = (diff.TransformDiff == DiffType.Identical) ? "✓" : "≈";
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = (diff.TransformDiff == DiffType.Identical) ? ComparisonStyles.TextGreen : ComparisonStyles.TextOrange;
            GUILayout.Label(statusIcon, style, GUILayout.Width(20));
            
            EditorGUILayout.EndHorizontal();

            if (wasExpanded)
            {
                EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(false));
                
                var tA = diff.ObjectA.transform;
                var tB = diff.ObjectB.transform;

                DrawRow("Position", diff.PositionDiff.ValueA, diff.PositionDiff.ValueB, diff.PositionDiff.Status, diff.PositionDiff, tA, tB);
                DrawRow("Rotation", diff.RotationDiff.ValueA, diff.RotationDiff.ValueB, diff.RotationDiff.Status, diff.RotationDiff, tA, tB);
                DrawRow("Scale", diff.ScaleDiff.ValueA, diff.ScaleDiff.ValueB, diff.ScaleDiff.Status, diff.ScaleDiff, tA, tB);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
        }

        private System.Collections.Generic.HashSet<string> collapsedComponents = new System.Collections.Generic.HashSet<string>();

        private void DrawComponentComparison(ComparisonResult result)
        {
            if (result.ComponentDifferences == null) return;

            foreach (var comp in result.ComponentDifferences)
            {
                DrawComponentDiff(comp);
            }
        }

        private void DrawComponentDiff(ComponentDiff diff)
        {
            // Unique key for foldout: Name + InstanceID (from A or B)
            string instanceId = (diff.ComponentA != null) ? diff.ComponentA.GetInstanceID().ToString() :
                               (diff.ComponentB != null) ? diff.ComponentB.GetInstanceID().ToString() : "";
            string key = $"{diff.ComponentName}_{instanceId}";
            bool wasCollapsed = drawingCollapsedComponents.Contains(key);
            bool isExpanded = !wasCollapsed;

            // ... (rest of the colors/icons)
            Color bgColor = Color.clear;
            string icon = "";
            Color iconColor = Color.white;

            switch (diff.Status)
            {
                case DiffType.Identical: icon = "✓"; iconColor = ComparisonStyles.TextGreen; break;
                case DiffType.Modified: bgColor = ComparisonStyles.ColorModified; icon = "⚠"; iconColor = ComparisonStyles.TextOrange; break;
                case DiffType.Missing: bgColor = ComparisonStyles.ColorMissing; icon = "✗"; iconColor = ComparisonStyles.TextRed; break;
                case DiffType.Added: bgColor = ComparisonStyles.ColorAdded; icon = "⊕"; iconColor = ComparisonStyles.TextBlue; break;
            }

            GUI.backgroundColor = (bgColor != Color.clear) ? bgColor : Color.white;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.backgroundColor = Color.white;

            bool nowExpanded = EditorGUILayout.Foldout(isExpanded, diff.ComponentName, true);
            if (nowExpanded != isExpanded)
            {
                if (nowExpanded) collapsedComponents.Remove(key);
                else collapsedComponents.Add(key);
            }

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = iconColor;
            GUILayout.Label(icon, style, GUILayout.Width(20));

            if (diff.Status == DiffType.Missing) GUILayout.Label("(Missing)", EditorStyles.miniLabel);
            else if (diff.Status == DiffType.Added) GUILayout.Label("(Added)", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                if (diff.Status == DiffType.Missing)
                {
                    EditorGUILayout.HelpBox("Component exists in Object A but is missing in Object B.", MessageType.Error);
                }
                else if (diff.Status == DiffType.Added)
                {
                    EditorGUILayout.HelpBox("Component is new in Object B (not in Object A).", MessageType.Info);
                }
                else if (diff.PropertyDifferences.Count > 0)
                {
                    EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(false));
                    foreach (var prop in diff.PropertyDifferences)
                    {
                        DrawRow(prop.DisplayName, prop.ValueA, prop.ValueB, prop.Status, prop, diff.ComponentA, diff.ComponentB);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(2);
            }
        }

        public System.Action<Component, Component, string, bool, string> OnSyncRequested;
        public System.Action<string> OnUndoRequested;

        private string GetPropKey(Component comp, string path)
        {
            if (comp == null) return "";
            return comp.GetInstanceID() + "_" + path;
        }

        private void DrawRow(string label, string valA, string valB, DiffType type, PropertyDiff propDiff = null, Component compA = null, Component compB = null, string iconName = "")
        {
            string key = propDiff != null ? GetPropKey(compA, propDiff.PropertyPath) : "";
            bool isSynced = !string.IsNullOrEmpty(key) && drawingSyncedPaths.Contains(key);

            // Skip if identical and filtering, UNLESS it was just synced
            if (drawingShowOnlyDifferences && type == DiffType.Identical && !isSynced)
                return;

            Color bg = ComparisonStyles.GetColorForDiff(type);
            GUI.backgroundColor = bg;
            EditorGUILayout.BeginHorizontal(ComparisonStyles.RowBackground);
            GUI.backgroundColor = Color.white;

            // Column 1: Label (with optional icon)
            EditorGUILayout.BeginHorizontal(GUILayout.Width(120), GUILayout.ExpandWidth(false));
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = EditorGUIUtility.IconContent(iconName);
                if (icon != null) GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(16));
            }
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Column 2: Value A
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(valA, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            // Column 3: Sync Control or Divider
            EditorGUILayout.BeginVertical(GUILayout.Width(120), GUILayout.ExpandWidth(false));
            
            if (isSynced)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Width(120));
                var c = GUI.color;
                GUI.color = ComparisonStyles.TextGreen;
                EditorGUILayout.LabelField("✓ Synced", ComparisonStyles.HeaderStyle, GUILayout.Width(70));
                GUI.color = c;

                if (GUILayout.Button("Undo", EditorStyles.miniButton, GUILayout.Width(45)))
                {
                    OnUndoRequested?.Invoke(key);
                    Event.current.Use();
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (type == DiffType.Modified && propDiff != null)
            {
                DrawSyncButtons(propDiff, compA, compB, key);
            }
            else if (type != DiffType.Identical)
            {
                EditorGUILayout.LabelField("≠", ComparisonStyles.HeaderStyle);
            }
            else
            {
                var c = GUI.color;
                GUI.color = ComparisonStyles.TextGreen;
                EditorGUILayout.LabelField("✓", ComparisonStyles.HeaderStyle);
                GUI.color = c;
            }
            EditorGUILayout.EndVertical();

            // Column 4: Value B
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(valB, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSyncButtons(PropertyDiff prop, Component cA, Component cB, string key)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(110), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            
            // A -> B Button
            if (GUILayout.Button("→", EditorStyles.miniButton, GUILayout.Height(16), GUILayout.Width(110)))
            {
                OnSyncRequested?.Invoke(cA, cB, prop.PropertyPath, true, key);
                Event.current.Use();
            }

            EditorGUILayout.Space(2);

            // B -> A Button
            if (GUILayout.Button("←", EditorStyles.miniButton, GUILayout.Height(16), GUILayout.Width(110)))
            {
                OnSyncRequested?.Invoke(cA, cB, prop.PropertyPath, false, key);
                Event.current.Use();
            }

            EditorGUILayout.EndVertical();
        }

    }
}
