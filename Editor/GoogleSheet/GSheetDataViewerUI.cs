using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.GoogleSheet.Editor
{
    [System.Serializable]
    public class GSheetDataViewerUI
    {
        private GoogleSheetSettings _settings;
        private Vector2 _scrollPosition;
        private GoogleSheetService _service;
        private bool _isFetching = false;
        private bool _showSetupGuide = false;
        
        private List<List<string>> _rawData = new List<List<string>>();
        private Dictionary<string, List<string>> _validationRules = new Dictionary<string, List<string>>();
        
        private Dictionary<string, string> _pendingUpdates = new Dictionary<string, string>();
        private double _nextSyncTime = 0;
        private const float SyncDelay = 1.0f;
        
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private Color _lineColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private Color _alternateRowColor = new Color(1f, 1f, 1f, 0.03f);
        private Dictionary<string, Vector2> _jsonScrollPositions = new Dictionary<string, Vector2>();

        public void Initialize()
        {
            LoadSettings();
            EditorApplication.update += HandlePendingUpdates;
        }

        public void OnDisable()
        {
            EditorApplication.update -= HandlePendingUpdates;
        }

        private void LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:GoogleSheetSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _settings = AssetDatabase.LoadAssetAtPath<GoogleSheetSettings>(path);
            }
        }

        public void Draw()
        {
            InitStyles();
            DrawToolbar();
            
            bool isConfigMissing = _settings == null || string.IsNullOrEmpty(_settings.SpreadsheetId) || 
                                   string.IsNullOrEmpty(_settings.ApiKey) || string.IsNullOrEmpty(_settings.ServiceAccountJson);
            
            if (_showSetupGuide || isConfigMissing)
            {
                DrawOnboarding(isConfigMissing);
                return;
            }

            if (_isFetching)
            {
                DrawLoadingOverlay();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawDataTable();
            EditorGUILayout.EndScrollView();
            
            DrawStatusBar();
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.toolbar)
                {
                    fixedHeight = 22,
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };

                _cellStyle = new GUIStyle(EditorStyles.textField)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 4, 2, 2)
                };
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(new GUIContent("  📊  GSheet Data Viewer"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_settings != null && !string.IsNullOrEmpty(_settings.SpreadsheetId))
            {
                if (GUILayout.Button(new GUIContent(" Refresh", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton))
                {
                    _showSetupGuide = false;
                    FetchData();
                }
            }

            if (GUILayout.Button(new GUIContent(" Help", EditorGUIUtility.IconContent("_Help").image), EditorStyles.toolbarButton))
            {
                _showSetupGuide = !_showSetupGuide;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDataTable()
        {
            if (_rawData == null || _rawData.Count == 0)
            {
                EditorGUILayout.HelpBox("No data loaded. Click Refresh to fetch data from your Google Sheet.", MessageType.Info);
                return;
            }

            int maxCols = 0;
            foreach (var row in _rawData)
            {
                if (row.Count > maxCols) maxCols = row.Count;
            }

            float colWidth = 150f;

            for (int i = 0; i < _rawData.Count; i++)
            {
                var row = _rawData[i];
                Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                
                if (i % 2 == 1)
                {
                    EditorGUI.DrawRect(rowRect, _alternateRowColor);
                }

                for (int j = 0; j < maxCols; j++)
                {
                    string value = j < row.Count ? row[j] : "";
                    Rect cellRect = GUILayoutUtility.GetRect(colWidth, 22);
                    string cellKey = $"{i}:{j}";
                    
                    if (_validationRules.ContainsKey(cellKey))
                    {
                        List<string> options = _validationRules[cellKey];
                        int currentIndex = options.IndexOf(value);
                        if (currentIndex == -1) currentIndex = 0;

                        EditorGUI.BeginChangeCheck();
                        int newIndex = EditorGUI.Popup(new Rect(cellRect.x + 2, cellRect.y + 2, cellRect.width - 4, cellRect.height - 4), currentIndex, options.ToArray());
                        if (EditorGUI.EndChangeCheck())
                        {
                            string newValue = options[newIndex];
                            while (row.Count <= j) row.Add("");
                            row[j] = newValue;
                            UpdateCellDebounced(i, j, newValue);
                        }
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        string newValue = EditorGUI.TextField(new Rect(cellRect.x + 2, cellRect.y + 2, cellRect.width - 4, cellRect.height - 4), value, EditorStyles.textField);
                        if (EditorGUI.EndChangeCheck())
                        {
                            while (row.Count <= j) row.Add("");
                            row[j] = newValue;
                            UpdateCellDebounced(i, j, newValue);
                        }
                    }

                    EditorGUI.DrawRect(new Rect(cellRect.xMax - 1, cellRect.y, 1, cellRect.height), _lineColor);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), _lineColor);
            }
        }

        private void UpdateCellDebounced(int rowIndex, int colIndex, string value)
        {
            if (_service == null || _settings == null) return;
            string columnLetter = GetColumnLetter(colIndex);
            string range = $"{columnLetter}{rowIndex + 1}"; 
            
            _pendingUpdates[range] = value;
            _nextSyncTime = EditorApplication.timeSinceStartup + SyncDelay;
        }

        private string GetColumnLetter(int index)
        {
            string column = "";
            while (index >= 0)
            {
                column = (char)('A' + (index % 26)) + column;
                index = (index / 26) - 1;
            }
            return column;
        }

        private void HandlePendingUpdates()
        {
            if (_pendingUpdates.Count > 0 && EditorApplication.timeSinceStartup > _nextSyncTime)
            {
                SyncAllPending();
            }
        }

        private async void SyncAllPending()
        {
            var updates = new Dictionary<string, string>(_pendingUpdates);
            _pendingUpdates.Clear();

            foreach (var kvp in updates)
            {
                bool success = await _service.UpdateCellAsync(kvp.Key, kvp.Value);
                if (!success) Debug.LogError($"[GSheet Data Viewer] Failed to sync {kvp.Key}");
            }
        }

        private async void FetchData()
        {
            if (_service == null) _service = new GoogleSheetService(_settings);
            _isFetching = true;
            try
            {
                var result = await _service.FetchDataWithValidationAsync();
                _rawData = result.data;
                _validationRules = result.validation;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GSheet Data Viewer] Error: {e.Message}");
            }
            finally
            {
                _isFetching = false;
            }
        }

        private void DrawLoadingOverlay()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Loading data from Google Sheets...", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            int count = _rawData != null ? _rawData.Count : 0;
            GUILayout.Label($"Rows: {count}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (_pendingUpdates.Count > 0)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("☁ Syncing...", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            GUILayout.Label($"Total Rows: {_rawData.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOnboarding(bool isConfigMissing)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 10, 20) });
            
            GUILayout.Label("GSheet Data Viewer Setup", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            // STEP 1: SETTINGS
            DrawStepCard(1, "Initialize Settings", _settings != null);
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("A Settings file is required to store your configuration.", MessageType.Info);
                if (GUILayout.Button("Create GoogleSheetSettings", GUILayout.Height(30)))
                {
                    CreateSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Settings Asset found and ready.", MessageType.None);
            }
            EditorGUILayout.Space(10);

            // STEP 2: SPREADSHEET ID
            bool hasId = _settings != null && !string.IsNullOrEmpty(_settings.SpreadsheetId);
            DrawStepCard(2, "Spreadsheet Identity", hasId);
            EditorGUILayout.LabelField("Paste the unique ID from your Google Sheet URL:", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label(".../spreadsheets/d/", EditorStyles.miniLabel);
            GUI.color = Color.cyan;
            GUILayout.Label("  18_u8BCc20T8QImjwa5VrnAGdeWOSj9R7ZKr4yjrpLDA", EditorStyles.boldLabel);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("/edit#gid=0", EditorStyles.miniLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();

            if (_settings != null)
            {
                _settings.SpreadsheetId = EditorGUILayout.TextField("Spreadsheet ID", _settings.SpreadsheetId);
                EditorUtility.SetDirty(_settings);
            }
            EditorGUILayout.Space(10);

            // STEP 3: API KEY
            bool hasApiKey = _settings != null && !string.IsNullOrEmpty(_settings.ApiKey);
            DrawStepCard(3, "API Credentials (Read)", hasApiKey);
            EditorGUILayout.LabelField("API Key is required to fetch data from the sheet.", EditorStyles.wordWrappedLabel);
            if (_settings != null)
            {
                _settings.ApiKey = EditorGUILayout.TextField("Google API Key", _settings.ApiKey);
                EditorUtility.SetDirty(_settings);
            }
            EditorGUILayout.Space(10);

            // STEP 4: SERVICE ACCOUNT
            bool hasServiceAccount = _settings != null && !string.IsNullOrEmpty(_settings.ServiceAccountJson);
            DrawStepCard(4, "Service Account (Write)", hasServiceAccount);
            EditorGUILayout.LabelField("Required for syncing Unity edits back to Google Sheets.", EditorStyles.wordWrappedLabel);
            if (_settings != null)
            {
                GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea) 
                { 
                    wordWrap = true,
                    padding = new RectOffset(5, 5, 5, 5)
                };
                
                // Use a static variable to persist scroll position
                if (!_jsonScrollPositions.ContainsKey("serviceAccount"))
                {
                    _jsonScrollPositions["serviceAccount"] = Vector2.zero;
                }
                
                _jsonScrollPositions["serviceAccount"] = EditorGUILayout.BeginScrollView(_jsonScrollPositions["serviceAccount"], GUILayout.Height(100));
                _settings.ServiceAccountJson = EditorGUILayout.TextArea(_settings.ServiceAccountJson, textAreaStyle, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                EditorUtility.SetDirty(_settings);
            }
            EditorGUILayout.Space(10);

            // STEP 5: CONFIGURATION
            DrawStepCard(5, "Sheet Configuration", true);
            if (_settings != null)
            {
                _settings.SheetName = EditorGUILayout.TextField("Tab Name", _settings.SheetName);
                _settings.Range = EditorGUILayout.TextField("Cell Range", _settings.Range);
                EditorUtility.SetDirty(_settings);
            }
            EditorGUILayout.Space(20);
            
            // ACTION BUTTON
            if (_settings != null && hasId && hasApiKey && hasServiceAccount)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("✓ CONNECT & SYNC NOW", GUILayout.Height(50)))
                {
                    _showSetupGuide = false;
                    FetchData();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("Waiting for Credentials...", GUILayout.Height(50));
                GUI.enabled = true;
                EditorGUILayout.HelpBox("Complete all steps to enable connection.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawStepCard(int num, string title, bool completed)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{num}.", EditorStyles.boldLabel, GUILayout.Width(20));
            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void CreateSettings()
        {
            string path = "Assets/GoogleSheetSettings.asset";
            GoogleSheetSettings asset = ScriptableObject.CreateInstance<GoogleSheetSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _settings = asset;
            Debug.Log($"Created GoogleSheetSettings at {path}");
        }
    }
}
