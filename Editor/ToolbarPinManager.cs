using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dhinesh.EditorTools.CustomTaskbar
{
    [Serializable]
    public class PinnedItem
    {
        public string guid;
        public string displayName;
        public string originalName;

        public PinnedItem(string guid, string displayName, string originalName)
        {
            this.guid = guid;
            this.displayName = displayName;
            this.originalName = originalName;
        }
    }

    [InitializeOnLoad]
    public static class ToolbarPinManager
    {
        private const string PREFS_KEY = "Dhinesh_ToolbarPinnedItems";
        public const int MaxToolbarPins = 10;
        private static List<PinnedItem> pinnedItems = new List<PinnedItem>();

        static ToolbarPinManager()
        {
            LoadPins();
        }

        public static IReadOnlyList<PinnedItem> PinnedItems => pinnedItems;

        public static void Pin(UnityEngine.Object obj)
        {
            if (obj == null) return;

            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            if (pinnedItems.Any(p => p.guid == id)) return;

            if (pinnedItems.Count >= MaxToolbarPins)
            {
                EditorUtility.DisplayDialog("Toolbar Limit Reached", 
                    $"You can only pin up to {MaxToolbarPins} items. Please unpin something first.", "OK");
                return;
            }

            pinnedItems.Add(new PinnedItem(id, string.Empty, obj.name));
            SavePins();
        }

        public static void Unpin(string guid)
        {
            pinnedItems.RemoveAll(p => p.guid == guid);
            SavePins();
        }

        public static bool IsPinned(UnityEngine.Object obj)
        {
            if (obj == null) return false;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            return pinnedItems.Any(p => p.guid == id);
        }

        private static void SavePins()
        {
            string json = JsonUtility.ToJson(new PinnedListWrapper { items = pinnedItems });
            EditorPrefs.SetString(PREFS_KEY, json);
            ToolbarExtender.Repaint();
        }

        private static void LoadPins()
        {
            string json = EditorPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json))
            {
                pinnedItems = new List<PinnedItem>();
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<PinnedListWrapper>(json);
                pinnedItems = wrapper.items ?? new List<PinnedItem>();
            }
            catch
            {
                pinnedItems = new List<PinnedItem>();
            }
        }

        public static UnityEngine.Object Resolve(string guid)
        {
            if (GlobalObjectId.TryParse(guid, out var gid))
            {
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            }
            return null;
        }

        [Serializable]
        private class PinnedListWrapper
        {
            public List<PinnedItem> items;
        }

        // --- Context Menu Items ---

        [MenuItem("GameObject/Pin to Toolbar", false, 10)]
        private static void PinGameObject(MenuCommand menuCommand)
        {
            Pin(menuCommand.context);
        }

        [MenuItem("GameObject/Pin to Toolbar", true)]
        private static bool PinGameObjectValidate()
        {
            return Selection.activeObject != null && !IsPinned(Selection.activeObject);
        }

        [MenuItem("Assets/Pin to Toolbar", false, 100)]
        private static void PinAsset()
        {
            Pin(Selection.activeObject);
        }

        [MenuItem("Assets/Pin to Toolbar", true)]
        private static bool PinAssetValidate()
        {
            return Selection.activeObject != null && !IsPinned(Selection.activeObject);
        }

        [MenuItem("GameObject/Unpin from Toolbar", false, 11)]
        private static void UnpinGameObject(MenuCommand menuCommand)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(menuCommand.context).ToString();
            Unpin(id);
        }

        [MenuItem("GameObject/Unpin from Toolbar", true)]
        private static bool UnpinGameObjectValidate()
        {
            return Selection.activeObject != null && IsPinned(Selection.activeObject);
        }

        [MenuItem("Assets/Unpin from Toolbar", false, 101)]
        private static void UnpinAsset()
        {
            foreach (var obj in Selection.objects)
            {
                string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
                Unpin(id);
            }
        }

        [MenuItem("Assets/Unpin from Toolbar", true)]
        private static bool UnpinAssetValidate()
        {
            return Selection.activeObject != null && IsPinned(Selection.activeObject);
        }
    }
}
