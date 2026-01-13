using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace UnityProductivityTools.SnapshotTool
{
    public static class SnapshotRestorer
    {
        public static void Restore(SnapshotRoot snapshot)
        {
            if (snapshot == null || snapshot.gameObjects == null) return;

            // Group all undo operations into one action ID
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Restore Snapshot: {snapshot.snapshotName}");

            Dictionary<int, GameObject> idToGameObject = new Dictionary<int, GameObject>();
            Dictionary<int, GameObjectData> idToData = new Dictionary<int, GameObjectData>();

            GameObject rootContainer = null;
            
            foreach (var data in snapshot.gameObjects)
            {
                GameObject go = null;
                
                // Try to find existing
                UnityEngine.Object foundObj = EditorUtility.InstanceIDToObject(data.instanceID);
                if (foundObj != null && foundObj is GameObject foundGO)
                {
                    go = foundGO;
                    // Record object before making changes (for name, active, tag, layer)
                    Undo.RecordObject(go, "Restore GameObject State");
                }

                bool isNew = false;
                if (go == null)
                {
                   // Fallback: Create new
                   if (rootContainer == null) 
                   {
                       rootContainer = new GameObject($"{snapshot.snapshotName}_Restored");
                       Undo.RegisterCreatedObjectUndo(rootContainer, "Create Restore Container");
                   }
                   
                   go = new GameObject(data.name);
                   Undo.RegisterCreatedObjectUndo(go, "Create Restored Object");
                   Undo.SetTransformParent(go.transform, rootContainer.transform, "Set Parent");
                   isNew = true;
                }
                
                // Restore basic properties
                if (IsTagDefined(data.tag)) go.tag = data.tag;
                go.layer = data.layer;
                if (go.activeSelf != data.isActive) go.SetActive(data.isActive);
                
                // Restore Transform
                Undo.RecordObject(go.transform, "Restore Transform");
                go.transform.position = data.position;
                go.transform.rotation = data.rotation;
                go.transform.localScale = data.scale;

                idToGameObject[data.instanceID] = go;
                idToData[data.instanceID] = data;
            }

            // Phase 2: Restore Hierarchy (Parenting)
            foreach (var kvp in idToGameObject)
            {
                int id = kvp.Key;
                GameObject go = kvp.Value;
                GameObjectData data = idToData[id];

                if (data.parentInstanceID != 0 && idToGameObject.ContainsKey(data.parentInstanceID))
                {
                    GameObject parentGO = idToGameObject[data.parentInstanceID];
                    if (go.transform.parent != parentGO.transform)
                    {
                        Undo.SetTransformParent(go.transform, parentGO.transform, "Restore Parent");
                    }
                }
            }

            // Phase 3: Restore Components
            foreach (var kvp in idToGameObject)
            {
                RestoreComponents(kvp.Value, idToData[kvp.Key]);
            }
            
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static bool IsTagDefined(string tag)
        {
            try { return !string.IsNullOrEmpty(tag) && !tag.Equals("Untagged"); } catch { return false; }
        }

        private static void RestoreComponents(GameObject go, GameObjectData data)
        {
            var existingComponents = go.GetComponents<Component>();
            var usedComponents = new HashSet<Component>();

            foreach (var compData in data.components)
            {
                if (compData.typeName == typeof(Transform).FullName || 
                    compData.typeName == typeof(RectTransform).FullName) 
                    continue;

                Type type = GetTypeByName(compData.typeName);
                if (type == null) continue;

                Component targetComp = null;

                foreach (var existing in existingComponents)
                {
                    if (existing != null && existing.GetType() == type && !usedComponents.Contains(existing))
                    {
                        targetComp = existing;
                        usedComponents.Add(existing);
                        break;
                    }
                }

                if (targetComp == null)
                {
                    targetComp = Undo.AddComponent(go, type);
                    // No need to add to usedComponents if we don't plan to reuse it again for this snapshot pass
                }

                if (targetComp != null)
                {
                    Undo.RecordObject(targetComp, "Restore Component Values");
                    ApplyProperties(targetComp, compData.properties);
                }
            }
        }

        private static void ApplyProperties(Component comp, List<PropertyData> properties)
        {
            Type type = comp.GetType();
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (var propData in properties)
            {
                if (propData.value == "null") continue;

                try
                {
                    // Try Field first
                    FieldInfo field = type.GetField(propData.name, flags);
                    if (field != null)
                    {
                        object value = ParseValue(propData.value, field.FieldType);
                        if (value != null) field.SetValue(comp, value);
                        continue;
                    }

                    // Try Property
                    PropertyInfo prop = type.GetProperty(propData.name, flags);
                    if (prop != null && prop.CanWrite)
                    {
                        object value = ParseValue(propData.value, prop.PropertyType);
                        if (value != null) prop.SetValue(comp, value);
                    }
                }
                catch (Exception e)
                {
                    // Debug.LogWarning($"Failed to set {propData.name} on {type.Name}: {e.Message}");
                }
            }
        }

        private static object ParseValue(string valueStr, Type targetType)
        {
            if (targetType == typeof(string)) return valueStr;
            if (targetType == typeof(int)) return int.Parse(valueStr);
            if (targetType == typeof(float)) return float.Parse(valueStr);
            if (targetType == typeof(bool)) return bool.Parse(valueStr);
            
            // Unity Types Parsing
            if (targetType == typeof(Vector3)) return ParseVector3(valueStr);
            if (targetType == typeof(Quaternion)) return ParseQuaternion(valueStr);
            if (targetType == typeof(Vector2)) return ParseVector2(valueStr);
            if (targetType == typeof(Color)) return ParseColor(valueStr);

            // Generic Type Converter
            try
            {
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return converter.ConvertFrom(valueStr);
                }
            }
            catch {}

            return null;
        }

        // --- Helpers for parsing Unity's ToString format "(x, y, z)" ---
        
        private static Vector3 ParseVector3(string s)
        {
            // Remove parenthesis
            s = s.Trim('(', ')');
            string[] parts = s.Split(',');
            if (parts.Length == 3 && 
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                return new Vector3(x, y, z);
            }
            return Vector3.zero;
        }

        private static Vector2 ParseVector2(string s)
        {
             s = s.Trim('(', ')');
            string[] parts = s.Split(',');
            if (parts.Length == 2 && 
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y))
            {
                 return new Vector2(x, y);
            }
            return Vector2.zero;
        }

        private static Quaternion ParseQuaternion(string s)
        {
            s = s.Trim('(', ')');
            string[] parts = s.Split(',');
            if (parts.Length == 4 && 
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z) &&
                float.TryParse(parts[3], out float w))
            {
                return new Quaternion(x, y, z, w);
            }
            return Quaternion.identity;
        }

        private static Color ParseColor(string s)
        {
            // RGBA(r, g, b, a) usually from ToString
            s = s.Replace("RGBA(", "").Replace(")", "");
            string[] parts = s.Split(',');
            if (parts.Length >= 3)
            {
                 float.TryParse(parts[0], out float r);
                 float.TryParse(parts[1], out float g);
                 float.TryParse(parts[2], out float b);
                 float a = 1f;
                 if (parts.Length > 3) float.TryParse(parts[3], out a);
                 return new Color(r, g, b, a);
            }
            return Color.white;
        }

        private static Type GetTypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = assembly.GetType(name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
