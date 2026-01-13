using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.SnapshotTool
{
    public class GameObjectSnapshotTool : EditorWindow
    {
        // Capture Settings
        private string snapshotNamePrefix = "Snapshot";
        private bool includeChildren = true;
        private const string SAVE_FOLDER = "Assets/Snapshots";

        // UI State
        private int selectedTab = 0;
        private readonly string[] tabs = { "Capture", "View & Restore" };

        // Viewer State
        private TextAsset selectedSnapshot;
        private SnapshotRoot currentData;
        private Vector2 scrollPos;
        private HashSet<GameObjectData> expandedObjects = new HashSet<GameObjectData>();

        [MenuItem("Tools/GameDevTools/Snapshot Manager")]
        public static void ShowWindow()
        {
            GetWindow<GameObjectSnapshotTool>("Snapshot Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("GameObject Snapshot Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();

            if (selectedTab == 0)
            {
                DrawCaptureUI();
            }
            else
            {
                DrawViewerUI();
            }
        }

        #region Capture UI & Logic

        private void DrawCaptureUI()
        {
            GUILayout.Label("Capture Settings", EditorStyles.boldLabel);
            
            snapshotNamePrefix = EditorGUILayout.TextField("Snapshot Name", snapshotNamePrefix);
            includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);

            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Selection", GUILayout.Height(40)))
            {
                CaptureSelection();
            }

            if (GUILayout.Button("Capture Active Scene", GUILayout.Height(30)))
            {
                CaptureScene();
            }
        }

        private void CaptureSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("No objects selected to snapshot.");
                return;
            }

            CreateSnapshot(selection, "Selection");
        }

        private void CaptureScene()
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            CreateSnapshot(rootObjects, "Scene");
        }

        private void CreateSnapshot(GameObject[] roots, string mode)
        {
            SnapshotRoot snapshot = new SnapshotRoot();
            snapshot.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            snapshot.snapshotName = $"{snapshotNamePrefix}_{mode}";

            foreach (var root in roots)
            {
                TraverseAndRecord(root, snapshot);
            }

            SaveSnapshot(snapshot);
        }

        private void TraverseAndRecord(GameObject obj, SnapshotRoot snapshot)
        {
            GameObjectData data = new GameObjectData();
            data.name = obj.name;
            data.instanceID = obj.GetInstanceID();
            data.parentInstanceID = obj.transform.parent ? obj.transform.parent.gameObject.GetInstanceID() : 0;
            data.isActive = obj.activeSelf;
            data.tag = obj.tag;
            data.layer = obj.layer;
            data.position = obj.transform.position;
            data.rotation = obj.transform.rotation;
            data.scale = obj.transform.localScale;

            Component[] components = obj.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                ComponentData compData = new ComponentData();
                compData.typeName = comp.GetType().FullName;
                
                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var fields = comp.GetType().GetFields(flags);
                var props = comp.GetType().GetProperties(flags);

                foreach (var field in fields)
                {
                    try
                    {
                        var val = field.GetValue(comp);
                        compData.properties.Add(new PropertyData 
                        { 
                            name = field.Name, 
                            value = val != null ? val.ToString() : "null", 
                            type = field.FieldType.Name 
                        });
                    }
                    catch { }
                }

                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    try
                    {
                        var val = prop.GetValue(comp);
                        compData.properties.Add(new PropertyData 
                        { 
                            name = prop.Name, 
                            value = val != null ? val.ToString() : "null", 
                            type = prop.PropertyType.Name 
                        });
                    }
                    catch { }
                }

                data.components.Add(compData);
            }

            snapshot.gameObjects.Add(data);

            if (includeChildren)
            {
                foreach (Transform child in obj.transform)
                {
                    data.childrenIDs.Add(child.gameObject.GetInstanceID());
                    TraverseAndRecord(child.gameObject, snapshot);
                }
            }
        }

        private void SaveSnapshot(SnapshotRoot snapshot)
        {
            string json = JsonUtility.ToJson(snapshot, true);
            
            if (!Directory.Exists(SAVE_FOLDER))
            {
                Directory.CreateDirectory(SAVE_FOLDER);
            }

            string fileName = $"{snapshotNamePrefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string path = Path.Combine(SAVE_FOLDER, fileName);

            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"Snapshot saved to: {path}");
            
            // Auto-load in viewer
            selectedSnapshot = asset;
            LoadSnapshot();
            selectedTab = 1; // Switch to viewer tab
            Repaint();
        }

        #endregion

        #region Viewer UI & Logic

        private void DrawViewerUI()
        {
            GUILayout.Label("Snapshot Viewer", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            selectedSnapshot = (TextAsset)EditorGUILayout.ObjectField("Snapshot File", selectedSnapshot, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSnapshot();
            }

            if (currentData != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label($"Name: {currentData.snapshotName}");
                GUILayout.Label($"Time: {currentData.timestamp}");
                GUILayout.Label($"Objects: {currentData.gameObjects.Count}");
                
                if (GUILayout.Button("Restore to Scene", GUILayout.Height(30)))
                {
                    SnapshotRestorer.Restore(currentData);
                    Debug.Log("Snapshot restored to scene.");
                }

                EditorGUILayout.Space();

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                foreach (var obj in currentData.gameObjects)
                {
                    DrawObjectData(obj);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void LoadSnapshot()
        {
            expandedObjects.Clear();
            if (selectedSnapshot == null)
            {
                currentData = null;
                return;
            }

            try
            {
                currentData = JsonUtility.FromJson<SnapshotRoot>(selectedSnapshot.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse snapshot: {e.Message}");
                currentData = null;
            }
        }

        private void DrawObjectData(GameObjectData obj)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            bool isExpanded = expandedObjects.Contains(obj);
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, obj.name, true);

            if (newExpanded != isExpanded)
            {
                if (newExpanded) expandedObjects.Add(obj);
                else expandedObjects.Remove(obj);
            }

            if (newExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Tag", obj.tag);
                EditorGUILayout.LabelField("Layer", obj.layer.ToString());
                EditorGUILayout.LabelField("Position", obj.position.ToString());
                
                if (obj.components.Count > 0)
                {
                    EditorGUILayout.LabelField("Components:", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    foreach (var comp in obj.components)
                    {
                        EditorGUILayout.LabelField(comp.typeName);
                        if (comp.properties.Count > 0)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var prop in comp.properties)
                            {
                                 EditorGUILayout.LabelField($"{prop.name}: {prop.value}");
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
