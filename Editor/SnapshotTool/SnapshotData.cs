using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProductivityTools.SnapshotTool
{
    [Serializable]
    public class SnapshotRoot
    {
        public string timestamp;
        public string snapshotName;
        public List<GameObjectData> gameObjects = new List<GameObjectData>();
    }

    [Serializable]
    public class GameObjectData
    {
        public string name;
        public int instanceID;
        public int parentInstanceID;
        public bool isActive;
        public string tag;
        public int layer;
        
        // Transform specifics
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public List<ComponentData> components = new List<ComponentData>();
        public List<int> childrenIDs = new List<int>();
    }

    [Serializable]
    public class ComponentData
    {
        public string typeName;
        public List<PropertyData> properties = new List<PropertyData>();
    }

    [Serializable]
    public class PropertyData
    {
        public string name;
        public string value; // Serialized as string for simplicity
        public string type;
    }
}
