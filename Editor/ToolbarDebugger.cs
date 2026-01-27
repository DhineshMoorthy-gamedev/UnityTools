using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;
using System;

public class ToolbarDebugger : EditorWindow
{
    [MenuItem("Window/Debug Toolbar")]
    public static void ShowWindow() => GetWindow<ToolbarDebugger>();

    void OnGUI()
    {
        if (GUILayout.Button("Log Toolbar Tree"))
        {
            LogToolbar();
        }
    }

    void LogToolbar()
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        var toolbars = Resources.FindObjectsOfTypeAll(type);
        if (toolbars.Length == 0) return;

        var rootField = type.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
        var root = rootField.GetValue(toolbars[0]) as VisualElement;
        
        Debug.Log("--- Toolbar Tree ---");
        DumpElement(root, 0);
    }

    void DumpElement(VisualElement el, int indent)
    {
        if (el == null) return;
        string space = new string(' ', indent * 2);
        Debug.Log($"{space}{el.name} ({el.GetType().Name}) - {el.layout}");
        for (int i = 0; i < el.childCount; i++)
        {
            DumpElement(el[i], indent + 1);
        }
    }
}
