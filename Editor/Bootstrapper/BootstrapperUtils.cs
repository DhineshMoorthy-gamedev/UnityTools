using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityProductivityTools.Bootstrapper.Editor
{
    public static class BootstrapperUtils
    {
        public static void CreateFolders()
        {
            string projectRoot = "Assets/_Project";
            CreateDirectory("Assets/ThirdParty");
            CreateDirectory("Assets/SandBox");
            
            CreateDirectory(projectRoot);
            CreateDirectory($"{projectRoot}/Scripts");
            CreateDirectory($"{projectRoot}/Scripts/Core");
            CreateDirectory($"{projectRoot}/Scripts/UI");
            CreateDirectory($"{projectRoot}/Scripts/Gameplay");
            
            CreateDirectory($"{projectRoot}/Art");
            CreateDirectory($"{projectRoot}/Art/Materials");
            CreateDirectory($"{projectRoot}/Art/Textures");
            CreateDirectory($"{projectRoot}/Art/Models");
            
            CreateDirectory($"{projectRoot}/Prefabs");
            CreateDirectory($"{projectRoot}/Scenes");
            CreateDirectory($"{projectRoot}/Audio");
            
            AssetDatabase.Refresh();
            Debug.Log("Bootstrapper: Folders Generated!");
        }

        private static void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void CreateScenes()
        {
            string scenePath = "Assets/_Project/Scenes";
            CreateDirectory(scenePath);

            // Save current scene if needed
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // Create Boot
                CreateScene($"{scenePath}/00_Boot.unity");
                // Create Menu
                CreateScene($"{scenePath}/01_MainMenu.unity");
                // Create Gameplay
                CreateScene($"{scenePath}/02_Gameplay.unity");
                
                // Add to Build Settings
                List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
                buildScenes.Add(new EditorBuildSettingsScene($"{scenePath}/00_Boot.unity", true));
                buildScenes.Add(new EditorBuildSettingsScene($"{scenePath}/01_MainMenu.unity", true));
                buildScenes.Add(new EditorBuildSettingsScene($"{scenePath}/02_Gameplay.unity", true));
                
                EditorBuildSettings.scenes = buildScenes.ToArray();
                
                AssetDatabase.Refresh();
                Debug.Log("Bootstrapper: Scenes Created and Added to Build Settings!");
                
                // Open Boot scene
                EditorSceneManager.OpenScene($"{scenePath}/00_Boot.unity");
            }
        }

        private static void CreateScene(string path)
        {
            if (File.Exists(path))
            {
                Debug.LogWarning($"Scene already exists: {path}");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }

        public static void CreateScript(string scriptName, string content)
        {
            string folder = "Assets/_Project/Scripts/Core";
            CreateDirectory(folder);
            string path = $"{folder}/{scriptName}.cs";
            
            if (File.Exists(path))
            {
                Debug.LogWarning($"Script already exists: {scriptName}.cs");
                return;
            }
            File.WriteAllText(path, content);
            Debug.Log($"Bootstrapper: Created {scriptName}.cs");
        }

        public static void GenerateSingleton()
        {
            CreateScript("Singleton", @"using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    obj.name = typeof(T).Name;
                    _instance = obj.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}");
        }

        public static void GenerateGameConstants()
        {
             CreateScript("GameConstants", @"public static class GameConstants
{
    public const string GAME_VERSION = ""1.0.0"";
    
    // Add your Tags, Layers, and other constants here
}");
        }

        public static void GenerateObjectPool()
        {
             CreateScript("ObjectPool", @"using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private T _prefab;
    private Queue<T> _pool = new Queue<T>();
    private Transform _parent;

    public ObjectPool(T prefab, int initialSize = 10, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initialSize; i++)
        {
            T obj = Object.Instantiate(_prefab, _parent);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public T Get()
    {
        if (_pool.Count == 0)
        {
            T obj = Object.Instantiate(_prefab, _parent);
            return obj;
        }
        
        T pooledObj = _pool.Dequeue();
        pooledObj.gameObject.SetActive(true);
        return pooledObj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }
}");
        }

        public static void GenerateDDOL()
        {
            CreateScript("DontDestroyOnLoad", @"using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}");
        }

        public static void GenerateExtensions()
        {
            CreateScript("Extensions", @"using UnityEngine;

public static class Extensions
{
    public static void ResetTransformation(this Transform t)
    {
        t.position = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }
    
    public static void SetAlpha(this SpriteRenderer sr, float alpha)
    {
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }
}");
        }

        public static void ApplyProjectSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true; // Use this key if available, otherwise just runInBackground
            
            // Can add more settings here like Company Name, Product Name prompts, etc.
            
            Debug.Log("Bootstrapper: Standard Project Settings Applied (Linear Color, Run in BG)");
        }
    }
}
