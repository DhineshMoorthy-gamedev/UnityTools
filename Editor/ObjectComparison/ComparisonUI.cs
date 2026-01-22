using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace UnityTools.ObjectComparison
{
    [System.Serializable]
    public class ComparisonUI
    {
        private GameObject objectA;
        private GameObject objectB;
        private Vector2 scrollPosition;
        
        private ComparisonEngine engine;
        private ComparisonResult currentResult;
        private ComparisonResult drawingResult;
        private ComparisonResultView resultView;
        private bool showOnlyDifferences = false;
        private bool drawingShowOnlyDifferences = false;
        private bool needsRefresh = false;

        private List<SyncLogEntry> syncHistory = new List<SyncLogEntry>();
        private Vector2 historyScroll;
        private bool showHistory = true;

        private struct SyncRequest {
            public Component cA, cB;
            public string path;
            public bool aToB;
            public string key;
        }
        private List<SyncRequest> pendingSyncs = new List<SyncRequest>();
        private List<string> pendingUndos = new List<string>();

        public void Initialize()
        {
            engine = new ComparisonEngine();
            resultView = new ComparisonResultView();
            resultView.OnSyncRequested = (cA, cB, path, aToB, key) => {
                pendingSyncs.Add(new SyncRequest { cA = cA, cB = cB, path = path, aToB = aToB, key = key });
                
                syncHistory.Add(new SyncLogEntry {
                    Timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
                    PropertyName = path.Contains(".") ? path.Substring(path.LastIndexOf('.') + 1) : path,
                    ComponentName = cA != null ? cA.GetType().Name : "GameObject",
                    Direction = aToB ? "A → B" : "B → A",
                    NewValue = ""
                });

                needsRefresh = true;
            };

            resultView.OnUndoRequested = (key) => {
                pendingUndos.Add(key);
                needsRefresh = true;
                
                syncHistory.Add(new SyncLogEntry {
                    Timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
                    PropertyName = "Undo",
                    ComponentName = "Action",
                    Direction = "Revert",
                    NewValue = ""
                });
            };
        }

        public void Draw(Rect position)
        {
            if (Event.current.type == EventType.Layout)
            {
                drawingResult = currentResult;

                if (pendingUndos.Count > 0)
                {
                    foreach (var key in pendingUndos)
                    {
                        resultView.SyncedPaths.Remove(key);
                    }
                    pendingUndos.Clear();
                    
                    EditorApplication.delayCall += () => {
                        Undo.PerformUndo();
                        needsRefresh = true;
                    };
                }

                if (pendingSyncs.Count > 0)
                {
                    foreach (var req in pendingSyncs)
                    {
                        if (req.cA is Transform tA && req.cB is Transform tB)
                            engine.SyncTransform(tA, tB, req.path, req.aToB);
                        else
                            engine.SyncProperty(req.cA, req.cB, req.path, req.aToB);
                        
                        resultView.SyncedPaths.Add(req.key);
                    }
                    pendingSyncs.Clear();
                    needsRefresh = true;
                }

                if (needsRefresh)
                {
                    RunComparison();
                    needsRefresh = false;
                }

                if (currentResult == null && objectA != null && objectB != null)
                {
                    RunComparison();
                }

                drawingShowOnlyDifferences = showOnlyDifferences;
            }

            if (drawingResult == null && currentResult != null) drawingResult = currentResult;
            if (resultView != null) resultView.ShowOnlyDifferences = drawingShowOnlyDifferences;

            DrawHeader();
            EditorGUILayout.Space(10);
            
            EditorGUI.BeginChangeCheck();
            DrawSelectionPanel();
            if (EditorGUI.EndChangeCheck())
            {
                resultView.SyncedPaths.Clear();
                syncHistory.Clear();
            }
            
            EditorGUILayout.Space(10);
            DrawToolbar();
            EditorGUILayout.Space(10);
            DrawResultsPanel(position);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Object Comparison Tool", ComparisonStyles.HeaderStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectionPanel()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Object A", EditorStyles.boldLabel);
            objectA = (GameObject)EditorGUILayout.ObjectField(objectA, typeof(GameObject), true);
            if (objectA != null)
            {
               EditorGUILayout.LabelField($"Layer: {LayerMask.LayerToName(objectA.layer)}");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Object B", EditorStyles.boldLabel);
            objectB = (GameObject)EditorGUILayout.ObjectField(objectB, typeof(GameObject), true);
            if (objectB != null)
            {
                EditorGUILayout.LabelField($"Layer: {LayerMask.LayerToName(objectB.layer)}");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("Refresh Comparison", EditorStyles.toolbarButton))
            {
                RunComparison();
            }

            EditorGUI.BeginChangeCheck();
            showOnlyDifferences = GUILayout.Toggle(showOnlyDifferences, "Show Only Differences", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck() && resultView != null)
            {
                resultView.ShowOnlyDifferences = showOnlyDifferences;
            }

            GUILayout.FlexibleSpace();
            showHistory = GUILayout.Toggle(showHistory, "Show History", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResultsPanel(Rect position)
        {
            if (drawingResult == null)
            {
                EditorGUILayout.HelpBox("Select two objects and click Refresh to start comparison.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (resultView != null)
            {
                resultView.Draw(drawingResult);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (showHistory)
            {
                DrawHistoryPanel();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHistoryPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            EditorGUILayout.LabelField("Recent Changes", EditorStyles.boldLabel);
            
            if (syncHistory.Count == 0)
            {
                EditorGUILayout.LabelField("History is empty", EditorStyles.miniLabel);
            }
            else
            {
                if (GUILayout.Button("Clear", EditorStyles.miniButton))
                {
                    syncHistory.Clear();
                }

                historyScroll = EditorGUILayout.BeginScrollView(historyScroll);
                for (int i = syncHistory.Count - 1; i >= 0; i--)
                {
                    var entry = syncHistory[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.PropertyName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(entry.Timestamp, EditorStyles.miniLabel, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField($"{entry.ComponentName} ({entry.Direction})", EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void RunComparison()
        {
            if (objectA == null || objectB == null)
            {
                return;
            }
            
            if (objectA == objectB)
            {
                return;
            }

            currentResult = engine.CompareGameObjects(objectA, objectB);
        }
    }
}
