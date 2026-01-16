using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityProductivityTools.AdvancedInspector
{
    public static class InspectorFavorites
    {
        private const string PROP_PREFS_KEY = "AdvancedInspector_PropFavorites_V2";
        
        private static List<FavoritePropertyData> favoriteProperties = new List<FavoritePropertyData>();

        [System.Serializable]
        public class FavoritePropertyData
        {
            public string guid;
            public string objectName;
            public string compType;
            public string propPath;

            public string ToKey() => $"{guid}|{objectName}|{compType}:{propPath}";
            
            public static FavoritePropertyData FromKey(string key)
            {
                try
                {
                    string[] mainParts = key.Split('|');
                    if (mainParts.Length < 3) return null;
                    
                    string[] propParts = mainParts[2].Split(':');
                    return new FavoritePropertyData()
                    {
                        guid = mainParts[0],
                        objectName = mainParts[1],
                        compType = propParts[0],
                        propPath = propParts[1]
                    };
                }
                catch { return null; }
            }
        }

        static InspectorFavorites()
        {
            Load();
        }

        // Property Favorites
        public static void AddPropertyFavorite(GameObject source, string compType, string propPath)
        {
            string guid = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
            string objName = source.name;

            if (!favoriteProperties.Any(p => p.guid == guid && p.compType == compType && p.propPath == propPath))
            {
                favoriteProperties.Add(new FavoritePropertyData()
                {
                    guid = guid,
                    objectName = objName,
                    compType = compType,
                    propPath = propPath
                });
                Save();
            }
        }

        public static void RemovePropertyFavorite(GameObject source, string compType, string propPath)
        {
            string guid = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
            favoriteProperties.RemoveAll(p => p.guid == guid && p.compType == compType && p.propPath == propPath);
            Save();
        }

        public static void RemovePropertyFavoriteByData(FavoritePropertyData data)
        {
            favoriteProperties.RemoveAll(p => p.guid == data.guid && p.compType == data.compType && p.propPath == data.propPath);
            Save();
        }

        public static bool IsPropertyFavorite(GameObject source, string compType, string propPath)
        {
            if (source == null) return false;
            string guid = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
            return favoriteProperties.Any(p => p.guid == guid && p.compType == compType && p.propPath == propPath);
        }

        public static List<FavoritePropertyData> GetPropertyFavorites()
        {
            return new List<FavoritePropertyData>(favoriteProperties);
        }

        private static void Save()
        {
            EditorPrefs.SetString(PROP_PREFS_KEY, string.Join(";", favoriteProperties.Select(p => p.ToKey())));
        }

        private static void Load()
        {
            string propStr = EditorPrefs.GetString(PROP_PREFS_KEY, "");
            favoriteProperties = propStr.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(key => FavoritePropertyData.FromKey(key))
                .Where(p => p != null)
                .ToList();
        }
    }
}
