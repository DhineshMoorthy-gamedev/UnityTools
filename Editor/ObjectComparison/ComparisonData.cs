using System.Collections.Generic;
using UnityEngine;

namespace UnityTools.ObjectComparison
{
    public enum DiffType
    {
        Identical,
        Modified,
        Missing,  // Exists in A, missing in B
        Added,    // Missing in A, exists in B
        PartialMatch // For complex objects with internal diffs
    }

    public class ComparisonResult
    {
        public ObjectDiff ObjectDiff;
        public List<ComponentDiff> ComponentDifferences = new List<ComponentDiff>();
        // public HierarchyDiff HierarchyDifferences; // Phase 1.4
    }

    public class ObjectDiff
    {
        public GameObject ObjectA;
        public GameObject ObjectB;
        
        public DiffType NameDiff;
        public DiffType TagDiff;
        public DiffType LayerDiff;
        public DiffType ActiveDiff;

        public DiffType TransformDiff;
        public PropertyDiff PositionDiff;
        public PropertyDiff RotationDiff;
        public PropertyDiff ScaleDiff;

        public List<ChildDiff> ChildDifferences = new List<ChildDiff>();
    }

    public class ChildDiff
    {
        public string Name;
        public DiffType Status;
    }

    public class ComponentDiff
    {
        public Component ComponentA;
        public Component ComponentB;
        public string ComponentName;
        public DiffType Status; // Identical, Modified, Missing, Added
        public List<PropertyDiff> PropertyDifferences = new List<PropertyDiff>();
        public bool IsExpanded = false;
    }

    public class PropertyDiff
    {
        public string PropertyPath;
        public string DisplayName;
        public DiffType Status;
        public string ValueA;
        public string ValueB;
    }

    public class SyncLogEntry
    {
        public string Timestamp;
        public string PropertyName;
        public string ComponentName;
        public string Direction;
        public string NewValue;
    }
}
