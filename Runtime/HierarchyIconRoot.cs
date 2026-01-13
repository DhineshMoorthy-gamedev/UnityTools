using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Game Dev Tools/Hierarchy Icon Root")]
public class HierarchyIconRoot : MonoBehaviour
{
    [HideInInspector]
    public List<HierarchyIconMarker> markers = new();
}
