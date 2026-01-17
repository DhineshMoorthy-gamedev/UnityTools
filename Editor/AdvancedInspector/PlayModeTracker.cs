using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityProductivityTools.AdvancedInspector
{
    [InitializeOnLoad]
    public static class PlayModeTracker
    {
        [System.Serializable]
        public class PropertyDelta
        {
            public string scenePath;  // Hierarchy path in scene (e.g., "Canvas/Panel/Button")
            public string objectName; // Object name for display
            public string compType;
            public string propPath;
            public string displayName;
            public string valueStr; // Store as string for JSON
            public SerializedPropertyType type;

            public string Key => $"{scenePath}:{compType}:{propPath}";

            public object GetValue()
            {
                try {
                    switch (type)
                    {
                        case SerializedPropertyType.Integer: return int.Parse(valueStr);
                        case SerializedPropertyType.Boolean: return bool.Parse(valueStr);
                        case SerializedPropertyType.Float: return float.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
                        case SerializedPropertyType.String: return valueStr;
                        case SerializedPropertyType.Color: return StringToColor(valueStr);
                        case SerializedPropertyType.Vector2: return StringToVector(valueStr);
                        case SerializedPropertyType.Vector3: return StringToVector(valueStr);
                        case SerializedPropertyType.Enum: return int.Parse(valueStr);
                        default: return null;
                    }
                } catch (System.Exception e) { 
                    Debug.LogWarning($"[AdvancedInspector] Failed to parse value '{valueStr}' of type {type}: {e.Message}");
                    return null; 
                }
            }

            public static string ValueToString(object val, SerializedPropertyType type)
            {
                if (val == null) return "";
                if (type == SerializedPropertyType.Color) return "#" + ColorUtility.ToHtmlStringRGBA((Color)val);
                if (type == SerializedPropertyType.Float) return ((float)val).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (type == SerializedPropertyType.Vector2) { Vector2 v = (Vector2)val; return $"{v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}"; }
                if (type == SerializedPropertyType.Vector3) { Vector3 v = (Vector3)val; return $"{v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{v.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}"; }
                return val.ToString();
            }

            private Color StringToColor(string s) { Color c; ColorUtility.TryParseHtmlString(s, out c); return c; }
            private object StringToVector(string s) {
                string[] p = s.Split('|');
                if (p.Length == 2) return new Vector2(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
                if (p.Length == 3) return new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
                throw new System.FormatException("Invalid vector format");
            }
        }


        private static Dictionary<string, PropertyDelta> pendingDeltas = new Dictionary<string, PropertyDelta>();
        private const string SESSION_PATH = "Temp/AdvancedInspector_PlayModeDeltas.json";

        private static string GetScenePath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        static PlayModeTracker()
        {
            LoadDeltas();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.postprocessModifications += OnUndoRedoOrPropertyChange;
        }

        private static UndoPropertyModification[] OnUndoRedoOrPropertyChange(UndoPropertyModification[] modifications)
        {
            if (!EditorApplication.isPlaying) return modifications;

            bool changed = false;
            foreach (var mod in modifications)
            {
                if (mod.currentValue == null) continue;
                
                Object target = mod.currentValue.target;
                if (target is Component comp)
                {
                    GameObject go = comp.gameObject;
                    string scenePath = GetScenePath(go);
                    string compType = target.GetType().FullName;
                    string path = mod.currentValue.propertyPath;
                    string key = $"{scenePath}:{compType}:{path}";

                    SerializedObject so = new SerializedObject(target);
                    SerializedProperty prop = so.FindProperty(path);
                    if (prop != null)
                    {
                        pendingDeltas[key] = new PropertyDelta()
                        {
                            scenePath = scenePath,
                            objectName = go.name,
                            compType = compType,
                            propPath = path,
                            displayName = prop.displayName,
                            valueStr = PropertyDelta.ValueToString(GetPropertyValue(prop), prop.propertyType),
                            type = prop.propertyType
                        };

                        changed = true;
                    }
                }
            }

            if (changed) SaveDeltas();
            return modifications;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                pendingDeltas.Clear();
                SaveDeltas();
            }
        }

        private static void SaveDeltas()
        {
            var list = pendingDeltas.Values.ToList();
            string json = JsonUtility.ToJson(new SerializationWrapper { list = list });
            System.IO.File.WriteAllText(SESSION_PATH, json);
        }

        private static void LoadDeltas()
        {
            if (System.IO.File.Exists(SESSION_PATH))
            {
                string json = System.IO.File.ReadAllText(SESSION_PATH);
                var wrapper = JsonUtility.FromJson<SerializationWrapper>(json);
                if (wrapper != null && wrapper.list != null)
                {
                    pendingDeltas = wrapper.list.ToDictionary(d => d.Key, d => d);
                }
            }
        }

        [System.Serializable]
        private class SerializationWrapper { public List<PropertyDelta> list; }


        public static void RecordChange(SerializedProperty prop)
        {
            if (!EditorApplication.isPlaying) return;

            Component comp = prop.serializedObject.targetObject as Component;
            if (comp == null) return;
            
            GameObject go = comp.gameObject;
            string scenePath = GetScenePath(go);
            string compType = comp.GetType().FullName;
            string path = prop.propertyPath;
            string key = $"{scenePath}:{compType}:{path}";
            
            pendingDeltas[key] = new PropertyDelta()
            {
                scenePath = scenePath,
                objectName = go.name,
                compType = compType,
                propPath = path,
                displayName = prop.displayName,
                valueStr = PropertyDelta.ValueToString(GetPropertyValue(prop), prop.propertyType),
                type = prop.propertyType
            };
            SaveDeltas();
        }


        public static List<PropertyDelta> GetPendingDeltas() => pendingDeltas.Values.ToList();

        public static void ApplyDelta(PropertyDelta delta)
        {
            try {
                if (EditorApplication.isPlaying) return;
                
                GameObject go = GameObject.Find(delta.scenePath);
                if (go != null)
                {
                    Component comp = go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == delta.compType);
                    if (comp != null)
                    {
                        SerializedObject so = new SerializedObject(comp);
                        SerializedProperty prop = so.FindProperty(delta.propPath);
                        if (prop != null)
                        {
                            Undo.RecordObject(comp, "Apply Play Mode Change");
                            object val = delta.GetValue();
                            if (val != null)
                            {
                                ApplyValueToProperty(prop, val, delta.type);
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(comp);
                                
                                if (PrefabUtility.IsPartOfPrefabInstance(comp))
                                {
                                    PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
                                }
                            }
                        }
                    }
                }
            } catch (System.Exception e) {
                Debug.LogError($"[AdvancedInspector] Error applying Play Mode change: {e.Message}");
            }
            
            pendingDeltas.Remove(delta.Key);
            SaveDeltas();
        }

        public static void DiscardDelta(string key) { pendingDeltas.Remove(key); SaveDeltas(); }
        public static void ClearDeltas() { pendingDeltas.Clear(); SaveDeltas(); }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue;
                case SerializedPropertyType.Vector2: return prop.vector2Value;
                case SerializedPropertyType.Vector3: return prop.vector3Value;
                case SerializedPropertyType.Enum: return prop.enumValueIndex;
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue;
                default: return null;
            }
        }

        private static void ApplyValueToProperty(SerializedProperty prop, object val, SerializedPropertyType type)
        {
            if (val == null) return;
            switch (type)
            {
                case SerializedPropertyType.Integer: prop.intValue = (int)val; break;
                case SerializedPropertyType.Boolean: prop.boolValue = (bool)val; break;
                case SerializedPropertyType.Float: prop.floatValue = (float)val; break;
                case SerializedPropertyType.String: prop.stringValue = (string)val; break;
                case SerializedPropertyType.Color: prop.colorValue = (Color)val; break;
                case SerializedPropertyType.Vector2: prop.vector2Value = (Vector2)val; break;
                case SerializedPropertyType.Vector3: prop.vector3Value = (Vector3)val; break;
                case SerializedPropertyType.Enum: prop.enumValueIndex = (int)val; break;
                case SerializedPropertyType.ObjectReference: prop.objectReferenceValue = (Object)val; break;
            }
        }
    }
}
