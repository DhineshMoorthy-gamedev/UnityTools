using UnityEngine;
using UnityEditor;

namespace UnityTools.ObjectComparison
{
    public class ComparisonEngine
    {
        public ComparisonResult CompareGameObjects(GameObject objA, GameObject objB)
        {
            var result = new ComparisonResult();
            result.ObjectDiff = new ObjectDiff();
            
            result.ObjectDiff.ObjectA = objA;
            result.ObjectDiff.ObjectB = objB;

            // Basic Properties
            result.ObjectDiff.NameDiff = (objA.name == objB.name) ? DiffType.Identical : DiffType.Modified;
            result.ObjectDiff.TagDiff = (objA.tag == objB.tag) ? DiffType.Identical : DiffType.Modified;
            result.ObjectDiff.LayerDiff = (objA.layer == objB.layer) ? DiffType.Identical : DiffType.Modified;
            result.ObjectDiff.ActiveDiff = (objA.activeSelf == objB.activeSelf) ? DiffType.Identical : DiffType.Modified;

            CompareTransform(objA.transform, objB.transform, result.ObjectDiff);
            
            CompareComponents(objA, objB, result);
            
            CompareHierarchy(objA, objB, result);

            return result;
        }

        private void CompareHierarchy(GameObject objA, GameObject objB, ComparisonResult result)
        {
            var diffs = result.ObjectDiff.ChildDifferences;
            var tA = objA.transform;
            var tB = objB.transform;
            
            // Collect children
            var childrenA = new System.Collections.Generic.List<Transform>();
            foreach(Transform t in tA) childrenA.Add(t);
            
            var childrenB = new System.Collections.Generic.List<Transform>();
            foreach(Transform t in tB) childrenB.Add(t);

            // Match A children in B
            // Using Name as key for now
            bool[] matchedB = new bool[childrenB.Count];

            foreach (var childA in childrenA)
            {
                var diff = new ChildDiff();
                diff.Name = childA.name;

                int matchIndex = -1;
                for(int j=0; j<childrenB.Count; j++)
                {
                    if(!matchedB[j] && childrenB[j].name == childA.name)
                    {
                        matchIndex = j;
                        break;
                    }
                }

                if (matchIndex != -1)
                {
                    matchIndex = matchIndex;
                    matchedB[matchIndex] = true;
                    diff.Status = DiffType.Identical; // Or check deep diff? For now just existence.
                }
                else
                {
                    diff.Status = DiffType.Missing;
                }
                diffs.Add(diff);
            }

            // Check added
            for(int j=0; j<childrenB.Count; j++)
            {
                if(!matchedB[j])
                {
                    var diff = new ChildDiff();
                    diff.Name = childrenB[j].name;
                    diff.Status = DiffType.Added;
                    diffs.Add(diff);
                }
            }
        }

        private void CompareTransform(Transform tA, Transform tB, ObjectDiff diff)
        {
            float tolerance = 0.0001f;

            diff.PositionDiff = new PropertyDiff {
                DisplayName = "Position",
                PropertyPath = "Position",
                ValueA = tA.localPosition.ToString(),
                ValueB = tB.localPosition.ToString(),
                Status = Vector3.Distance(tA.localPosition, tB.localPosition) < tolerance ? DiffType.Identical : DiffType.Modified
            };

            diff.RotationDiff = new PropertyDiff {
                DisplayName = "Rotation",
                PropertyPath = "Rotation",
                ValueA = tA.localRotation.eulerAngles.ToString(),
                ValueB = tB.localRotation.eulerAngles.ToString(),
                Status = Quaternion.Angle(tA.localRotation, tB.localRotation) < 0.1f ? DiffType.Identical : DiffType.Modified
            };

            diff.ScaleDiff = new PropertyDiff {
                DisplayName = "Scale",
                PropertyPath = "Scale",
                ValueA = tA.localScale.ToString(),
                ValueB = tB.localScale.ToString(),
                Status = Vector3.Distance(tA.localScale, tB.localScale) < tolerance ? DiffType.Identical : DiffType.Modified
            };

            if (diff.PositionDiff.Status == DiffType.Identical && 
                diff.RotationDiff.Status == DiffType.Identical && 
                diff.ScaleDiff.Status == DiffType.Identical)
            {
                diff.TransformDiff = DiffType.Identical;
            }
            else
            {
                diff.TransformDiff = DiffType.Modified;
            }
        }

        public void CompareComponents(GameObject objA, GameObject objB, ComparisonResult result)
        {
            var compsA = objA.GetComponents<Component>();
            var compsB = objB.GetComponents<Component>();
            
            // Simple matching by type and occurrence index for now.
            // A robust solution would try to fuzzy match, but type+index is standard for Unity serialization
            
            // Track which B components we've matched to specific A components
            bool[] matchedB = new bool[compsB.Length];

            // 1. Find matches for A in B
            for (int i = 0; i < compsA.Length; i++)
            {
                var cA = compsA[i];
                if(cA is Transform) continue; // Handled separately

                var diff = new ComponentDiff();
                diff.ComponentName = cA.GetType().Name;

                // Find matching component in B
                int matchIndex = -1;
                for(int j=0; j<compsB.Length; j++)
                {
                    if(!matchedB[j] && compsB[j].GetType() == cA.GetType())
                    {
                        matchIndex = j;
                        break;
                    }
                }

                if (matchIndex != -1)
                {
                    // Found match
                    var cB = compsB[matchIndex];
                    matchedB[matchIndex] = true;
                    
                    diff.ComponentA = cA;
                    diff.ComponentB = cB;
                    
                    CompareSerializedComponents(cA, cB, diff);
                }
                else
                {
                    // Missing in B
                    diff.ComponentA = cA;
                    diff.Status = DiffType.Missing;
                }

                result.ComponentDifferences.Add(diff);
            }

            // 2. Find remaining B components (Added)
            for (int j = 0; j < compsB.Length; j++)
            {
                if (!matchedB[j])
                {
                    var cB = compsB[j];
                    if(cB is Transform) continue;

                    var diff = new ComponentDiff();
                    diff.ComponentB = cB;
                    diff.ComponentName = cB.GetType().Name;
                    diff.Status = DiffType.Added;
                    result.ComponentDifferences.Add(diff);
                }
            }
        }

        public void SyncProperty(Component compA, Component compB, string propertyPath, bool aToB)
        {
            if (compA == null || compB == null) return;

            Component source = aToB ? compA : compB;
            Component target = aToB ? compB : compA;

            Undo.RecordObject(target, $"Sync {propertyPath} from {source.gameObject.name}");

            SerializedObject soSource = new SerializedObject(source);
            SerializedObject soTarget = new SerializedObject(target);

            SerializedProperty propSource = soSource.FindProperty(propertyPath);
            SerializedProperty propTarget = soTarget.FindProperty(propertyPath);

            if (propSource != null && propTarget != null)
            {
                // Note: SerializedProperty.DataEquals and property assignment
                // We copy the raw value. Using duplicateCommand or generic copy is hard,
                // but for most serialized types this works:
                
                // For complex types, iterative copy is better, but SerializedProperty.CopyValue (custom)
                // Actually SerializedProperty doesn't have a direct "CopyTo". 
                // We'll use a helper or specific type handling.
                CopySerializedProperty(propSource, propTarget);
                
                soTarget.ApplyModifiedProperties();
            }
        }

        private void CopySerializedProperty(SerializedProperty source, SerializedProperty target)
        {
            // This is a simplified version. Robust copy for all Unity types:
            switch (source.propertyType)
            {
                case SerializedPropertyType.Integer: target.intValue = source.intValue; break;
                case SerializedPropertyType.Boolean: target.boolValue = source.boolValue; break;
                case SerializedPropertyType.Float: target.floatValue = source.floatValue; break;
                case SerializedPropertyType.String: target.stringValue = source.stringValue; break;
                case SerializedPropertyType.Color: target.colorValue = source.colorValue; break;
                case SerializedPropertyType.ObjectReference: target.objectReferenceValue = source.objectReferenceValue; break;
                case SerializedPropertyType.LayerMask: target.intValue = source.intValue; break;
                case SerializedPropertyType.Enum: target.enumValueIndex = source.enumValueIndex; break;
                case SerializedPropertyType.Vector2: target.vector2Value = source.vector2Value; break;
                case SerializedPropertyType.Vector3: target.vector3Value = source.vector3Value; break;
                case SerializedPropertyType.Rect: target.rectValue = source.rectValue; break;
                case SerializedPropertyType.ArraySize: target.arraySize = source.arraySize; break;
                case SerializedPropertyType.AnimationCurve: target.animationCurveValue = source.animationCurveValue; break;
                case SerializedPropertyType.Bounds: target.boundsValue = source.boundsValue; break;
                case SerializedPropertyType.Gradient: 
                    // Gradient is tricky, usually requires reflection or specific methods
                    break;
                case SerializedPropertyType.Quaternion: target.quaternionValue = source.quaternionValue; break;
                default:
                    // If it's a generic/custom struct, we may need to iterate children
                    if (source.hasChildren)
                    {
                        var sourceIter = source.Copy();
                        var targetIter = target.Copy();
                        int depth = sourceIter.depth;
                        while (sourceIter.NextVisible(true) && sourceIter.depth > depth)
                        {
                            targetIter.NextVisible(true);
                            CopySerializedProperty(sourceIter, targetIter);
                        }
                    }
                    break;
            }
        }

        private void CompareSerializedComponents(Component cA, Component cB, ComponentDiff diff)
        {
            SerializedObject soA = new SerializedObject(cA);
            SerializedObject soB = new SerializedObject(cB);

            SerializedProperty propA = soA.GetIterator();
            SerializedProperty propB = soB.GetIterator();

            bool nextA = propA.NextVisible(true);
            bool nextB = propB.NextVisible(true);
            
            bool anyModified = false;

            while (nextA && nextB)
            {
                // Check if paths match (should always match for same component type)
                if (propA.propertyPath != propB.propertyPath)
                {
                    // Desync happened? Should be rare for same Type
                    break;
                }
                
                // Skip script reference
                if (propA.name == "m_Script")
                {
                    nextA = propA.NextVisible(false);
                    nextB = propB.NextVisible(false);
                    continue;
                }

                bool isMatch = SerializedProperty.DataEquals(propA, propB);
                
                var pDiff = new PropertyDiff();
                pDiff.PropertyPath = propA.propertyPath;
                pDiff.DisplayName = propA.displayName;
                pDiff.Status = isMatch ? DiffType.Identical : DiffType.Modified;
                
                pDiff.ValueA = GetValueString(propA);
                pDiff.ValueB = GetValueString(propB);

                diff.PropertyDifferences.Add(pDiff);
                if (!isMatch) anyModified = true;

                nextA = propA.NextVisible(false);
                nextB = propB.NextVisible(false);
            }

            diff.Status = anyModified ? DiffType.Modified : DiffType.Identical;
        }

        private string GetValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F3");
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum: return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.LayerMask: return prop.intValue.ToString();
                default: return "(Complex/Check Editor)";
            }
        }

        public void SyncTransform(Transform tA, Transform tB, string propName, bool aToB)
        {
            Transform source = aToB ? tA : tB;
            Transform target = aToB ? tB : tA;

            Undo.RecordObject(target, $"Sync Transform {propName}");

            if (propName == "Position") target.localPosition = source.localPosition;
            else if (propName == "Rotation") target.localRotation = source.localRotation;
            else if (propName == "Scale") target.localScale = source.localScale;
        }
    }
}
