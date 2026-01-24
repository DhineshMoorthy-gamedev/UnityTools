using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.GoogleSheet.Editor
{
    public class GoogleSheetEditorWindow : EditorWindow
    {
        private GoogleSheetSettings _settings;
        private Vector2 _scrollPosition;
        private GoogleSheetService _service;
        private bool _isFetching = false;
        private bool _showSetupGuide = false;
        private Vector2 _jsonScrollPosition;
        
        // Data & Validation
        private List<List<string>> _rawData = new List<List<string>>();
        private Dictionary<string, List<string>> _validationRules = new Dictionary<string, List<string>>();
        
        // Debounced Syncing
        private Dictionary<string, string> _pendingUpdates = new Dictionary<string, string>();
        private double _nextSyncTime = 0;
        private const float SyncDelay = 1.0f;
        
        // UI Styles
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _gridRowStyle;
        private GUIStyle _onboardingHeaderStyle;
        private Color _lineColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private Color _alternateRowColor = new Color(1f, 1f, 1f, 0.03f);

        [MenuItem("Tools/GameDevTools/GSheet Data Viewer", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<GoogleSheetEditorWindow>("GSheet Data Viewer");
            window.minSize = new Vector2(600, 500);
            window.position = new Rect(100, 100, 600, 550);
        }

        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.update += HandlePendingUpdates;
        }

        private void OnDisable()
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

            if (_settings != null)
            {
                _service = new GoogleSheetService(_settings);
            }
        }

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10,
                fixedHeight = 20,
                normal = { textColor = Color.gray }
            };

            _cellStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                padding = new RectOffset(5, 5, 2, 2)
            };
            
            _gridRowStyle = new GUIStyle();
            _gridRowStyle.padding = new RectOffset(0, 0, 0, 0);
            _gridRowStyle.margin = new RectOffset(0, 0, 0, 0);

            _onboardingHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            InitStyles();
            DrawToolbar();

            bool isConfigMissing = _settings == null || string.IsNullOrEmpty(_settings.SpreadsheetId) || string.IsNullOrEmpty(_settings.ApiKey) || string.IsNullOrEmpty(_settings.ServiceAccountJson);
            
            if (_showSetupGuide || isConfigMissing)
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                DrawOnboarding(isConfigMissing);
                EditorGUILayout.EndScrollView();
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
            Repaint();
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

            if (GUILayout.Button(new GUIContent(" Help", EditorGUIUtility.IconContent("Help").image), EditorStyles.toolbarButton))
            {
                _showSetupGuide = !_showSetupGuide;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOnboarding(bool isConfigMissing)
        {
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 10, 20) });
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent(" GSheet Data Viewer Setup", EditorGUIUtility.IconContent("d_BuildSettings.Editor.Small").image), _onboardingHeaderStyle);
            GUILayout.FlexibleSpace();
            if (!isConfigMissing)
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("winbtn_win_close"), GUIStyle.none, GUILayout.Width(20))) _showSetupGuide = false;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);

            // STEP 1: SETTINGS
            BeginStepCard(1, "Initialize Settings", _settings != null);
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("A Settings file is required to store your configuration.", MessageType.Info);
                if (GUILayout.Button("Create GoogleSheetSettings", GUILayout.Height(30))) CreateSettings();
            }
            else
            {
                EditorGUILayout.HelpBox("Settings Asset found and ready.", MessageType.None);
            }
            EndStepCard();

            // STEP 2: SPREADSHEET ID
            bool hasId = _settings != null && !string.IsNullOrEmpty(_settings.SpreadsheetId);
            BeginStepCard(2, "Spreadsheet Identity", hasId);
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
            }
            EndStepCard();

            // STEP 3: API KEY
            bool hasApiKey = _settings != null && !string.IsNullOrEmpty(_settings.ApiKey);
            BeginStepCard(3, "API Credentials (Read)", hasApiKey);
            EditorGUILayout.LabelField("API Key is required to fetch data from the sheet.", EditorStyles.wordWrappedLabel);
            if (_settings != null)
            {
                _settings.ApiKey = EditorGUILayout.TextField("Google API Key", _settings.ApiKey);
            }
            EndStepCard();

            // STEP 4: SERVICE ACCOUNT
            bool hasServiceAccount = _settings != null && !string.IsNullOrEmpty(_settings.ServiceAccountJson);
            BeginStepCard(4, "Service Account (Write)", hasServiceAccount);
            EditorGUILayout.LabelField("Required for syncing Unity edits back to Google Sheets.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(2);
            
            if (_settings != null)
            {
                GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea) 
                { 
                    wordWrap = true,
                    padding = new RectOffset(5, 5, 5, 5)
                };
                
                _jsonScrollPosition = EditorGUILayout.BeginScrollView(_jsonScrollPosition, GUILayout.Height(120));
                _settings.ServiceAccountJson = EditorGUILayout.TextArea(_settings.ServiceAccountJson, textAreaStyle, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("  Auth Setup Guide", EditorGUIUtility.IconContent("d_Help").image), GUILayout.Height(28)))
                {
                    Application.OpenURL("file:///C:/Users/DHINESH/.gemini/antigravity/brain/dc98d47f-e003-4619-9556-b5e5fb8d631c/google_cloud_setup_guide.md");
                }
                EditorGUILayout.EndHorizontal();
            }
            EndStepCard();

            // STEP 5: CONFIGURATION
            BeginStepCard(5, "Sheet Configuration", true);
            if (_settings != null)
            {
                _settings.SheetName = EditorGUILayout.TextField("Tab Name", _settings.SheetName);
                _settings.Range = EditorGUILayout.TextField("Cell Range", _settings.Range);
            }
            EndStepCard();

            EditorGUILayout.Space(30);
            
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

            EditorGUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        private void BeginStepCard(int num, string title, bool completed)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label($"{num}.", EditorStyles.boldLabel, GUILayout.Width(20));
            
            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(25, 10, 0, 5) });
        }

        private void EndStepCard()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawLoadingOverlay()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Label(rect, "Connecting to Google...", EditorStyles.centeredGreyMiniLabel);
            Repaint();
        }

        private void DrawDataTable()
        {
            if (_rawData == null || _rawData.Count == 0)
            {
                EditorGUILayout.HelpBox("No data found. If your sheet is not empty, click Refresh or check your Settings.", MessageType.Info);
                return;
            }

            int maxCols = 0;
            foreach (var r in _rawData) if (r.Count > maxCols) maxCols = r.Count;
            if (maxCols == 0) maxCols = 5; 

            float rowNumWidth = 30f;
            float colWidth = 120f;

            // HEADER ROW (A, B, C...)
            Rect headerRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            Rect cornerRect = GUILayoutUtility.GetRect(rowNumWidth, 20);
            EditorGUI.DrawRect(new Rect(cornerRect.xMax - 1, cornerRect.y, 1, cornerRect.height), _lineColor);

            for (int j = 0; j < maxCols; j++)
            {
                Rect hCellRect = GUILayoutUtility.GetRect(colWidth, 20);
                GUI.Label(hCellRect, GetColumnLetter(j), _headerStyle);
                EditorGUI.DrawRect(new Rect(hCellRect.xMax - 1, hCellRect.y, 1, hCellRect.height), _lineColor);
            }
            EditorGUILayout.EndHorizontal();

            // DATA ROWS
            for (int i = 0; i < _rawData.Count; i++)
            {
                List<string> row = _rawData[i];
                Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                
                if (i % 2 == 1) EditorGUI.DrawRect(rowRect, _alternateRowColor);

                // Row Number (1, 2, 3...)
                Rect numRect = GUILayoutUtility.GetRect(rowNumWidth, 22);
                GUI.Label(numRect, (i + 1).ToString(), _headerStyle);
                EditorGUI.DrawRect(new Rect(numRect.xMax - 1, numRect.y, 1, numRect.height), _lineColor);

                for (int j = 0; j < maxCols; j++)
                {
                    string value = j < row.Count ? row[j] : "";
                    Rect cellRect = GUILayoutUtility.GetRect(colWidth, 22);
                    string cellKey = $"{i}:{j}";
                    
                    if (_validationRules.ContainsKey(cellKey))
                    {
                        // RENDER DROPDOWN
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
                        // RENDER TEXT FIELD
                        EditorGUI.BeginChangeCheck();
                        string newValue = EditorGUI.TextField(new Rect(cellRect.x + 2, cellRect.y + 2, cellRect.width - 4, cellRect.height - 4), value, EditorStyles.textField);
                        if (EditorGUI.EndChangeCheck())
                        {
                            while (row.Count <= j) row.Add("");
                            row[j] = newValue;
                            UpdateCellDebounced(i, j, newValue);
                        }
                    }

                    // Vertical Line
                    EditorGUI.DrawRect(new Rect(cellRect.xMax - 1, cellRect.y, 1, cellRect.height), _lineColor);
                }
                EditorGUILayout.EndHorizontal();
                
                // Horizontal Line
                Rect lastRect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(lastRect.x, lastRect.yMax - 1, lastRect.width, 1), _lineColor);
            }
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(18));
            int count = _rawData != null ? _rawData.Count : 0;
            GUILayout.Label($"Rows: {count} | Last Updated: {System.DateTime.Now:HH:mm:ss}", EditorStyles.miniLabel);
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

        private void UpdateCellDebounced(int rowIndex, int colIndex, string value)
        {
            if (_service == null || _settings == null) return;
            string columnLetter = GetColumnLetter(colIndex);
            string range = $"{columnLetter}{rowIndex + 1}"; 
            
            _pendingUpdates[range] = value;
            _nextSyncTime = EditorApplication.timeSinceStartup + SyncDelay;
            Repaint();
        }

        private string GetColumnLetter(int index)
        {
            string letter = "";
            while (index >= 0)
            {
                letter = (char)('A' + (index % 26)) + letter;
                index = (index / 26) - 1;
            }
            return letter;
        }

        private async void FetchData()
        {
            if (_service == null) _service = new GoogleSheetService(_settings);
            _isFetching = true;
            Repaint();
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
                Repaint();
            }
        }

        private void CreateSettings()
        {
            _settings = CreateInstance<GoogleSheetSettings>();
            string path = "Assets/Editor/Resources/GoogleSheetSettings.asset";
            if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(_settings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
