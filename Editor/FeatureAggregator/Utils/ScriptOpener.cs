using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatureAggregator.Utils
{
    public static class ScriptOpener
    {
        public static void OpenScriptsInIDE(List<MonoScript> scripts)
        {
            foreach (var script in scripts)
            {
                if (script != null)
                {
                    AssetDatabase.OpenAsset(script);
                }
            }
        }
        
        public static void OpenObjects(List<Object> objects)
        {
            foreach (var obj in objects)
            {
                 if (obj != null)
                {
                    AssetDatabase.OpenAsset(obj);
                }
            }
        }
    }
}
