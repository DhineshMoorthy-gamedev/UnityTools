using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Bootstrapper.Editor
{
    [System.Serializable]
    public class BootstrapperUI
    {
        private bool _grpFolders = true;
        private bool _grpPackages = true;
        private bool _grpScenes = true;
        
        // Script generation toggles
        private bool _genSingleton = true;
        private bool _genConstants = true;
        private bool _genPool = false;
        private bool _genDDOL = false;
        private bool _genExtensions = false;

        public void Draw()
        {
            GUILayout.Label("Project Setup Assistant", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 1. Folders
            _grpFolders = EditorGUILayout.BeginFoldoutHeaderGroup(_grpFolders, "1. Project Structure");
            if (_grpFolders)
            {
                EditorGUILayout.HelpBox("Generates standard folder hierarchy (_Project, ThirdParty, SandBox, etc.)", MessageType.Info);
                if (GUILayout.Button("Generate Standard Folders"))
                {
                    BootstrapperUtils.CreateFolders();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space();

            // 2. Utility Files -> Base Scripts
            _grpPackages = EditorGUILayout.BeginFoldoutHeaderGroup(_grpPackages, "2. Base Scripts");
            if (_grpPackages)
            {
                EditorGUILayout.HelpBox("Select scripts to generate in _Project/Scripts/Core.", MessageType.None);
                
                _genSingleton = EditorGUILayout.Toggle("Singleton<T>", _genSingleton);
                _genConstants = EditorGUILayout.Toggle("GameConstants", _genConstants);
                _genPool = EditorGUILayout.Toggle("ObjectPool<T>", _genPool);
                _genDDOL = EditorGUILayout.Toggle("DontDestroyOnLoad", _genDDOL);
                _genExtensions = EditorGUILayout.Toggle("Extensions", _genExtensions);

                if (GUILayout.Button("Generate Selected Scripts"))
                {
                    if (_genSingleton) BootstrapperUtils.GenerateSingleton();
                    if (_genConstants) BootstrapperUtils.GenerateGameConstants();
                    if (_genPool) BootstrapperUtils.GenerateObjectPool();
                    if (_genDDOL) BootstrapperUtils.GenerateDDOL();
                    if (_genExtensions) BootstrapperUtils.GenerateExtensions();
                    
                    AssetDatabase.Refresh();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            // 3. Project Settings
            _grpScenes = EditorGUILayout.BeginFoldoutHeaderGroup(_grpScenes, "3. Project Settings");
            if (_grpScenes)
            {
                EditorGUILayout.HelpBox("Apply standard settings (Linear Color Space, Run In Background).", MessageType.Info);
                if (GUILayout.Button("Apply Standard Settings"))
                {
                    BootstrapperUtils.ApplyProjectSettings();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space();

            // 4. Scenes (Moved down)
            EditorGUILayout.LabelField("4. Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates Boot, Menu, and Gameplay scenes and adds them to Build Settings.", MessageType.Info);
            if (GUILayout.Button("Create Standard Scenes"))
            {
                BootstrapperUtils.CreateScenes();
            }
        }
    }
}
