using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTools
{
    /// <summary>
    /// A simple component for adding notes and comments to GameObjects in the Unity Editor.
    /// Useful for documenting your scene hierarchy and leaving reminders for yourself or team members.
    /// </summary>
    [AddComponentMenu("Game Dev Tools/Note")]
    [HelpURL("https://docs.unity3d.com/Manual/")]
    public class Note : MonoBehaviour
    {
        [Header("Note Settings")]
        [Tooltip("The title or summary of this note")]
        public string title = "Note";

        [TextArea(3, 10)]
        [Tooltip("Your note content - visible in the Inspector")]
        public string content = "Add your notes here...";

        [Space(10)]
        [Header("Visual Options")]
        public Color gizmoColor = Color.yellow;

        [Tooltip("Show an icon in the Scene view")]
        public bool showGizmo = true;

        [Range(0.1f, 2f)]
        [Tooltip("Size of the gizmo icon")]
        public float gizmoSize = 0.5f;

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawIcon(transform.position, "d_UnityEditor.InspectorWindow", true);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoSize);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Note))]
    public class NoteEditor : Editor
    {
        private SerializedProperty titleProp;
        private SerializedProperty contentProp;
        private SerializedProperty gizmoColorProp;
        private SerializedProperty showGizmoProp;
        private SerializedProperty gizmoSizeProp;

        private void OnEnable()
        {
            titleProp = serializedObject.FindProperty("title");
            contentProp = serializedObject.FindProperty("content");
            gizmoColorProp = serializedObject.FindProperty("gizmoColor");
            showGizmoProp = serializedObject.FindProperty("showGizmo");
            gizmoSizeProp = serializedObject.FindProperty("gizmoSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);

            // Title with icon
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("📝", "Note Title"), GUILayout.Width(20));
            titleProp.stringValue = EditorGUILayout.TextField(titleProp.stringValue, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Content area with custom style
            GUIStyle contentStyle = new GUIStyle(EditorStyles.textArea);
            contentStyle.wordWrap = true;
            contentStyle.padding = new RectOffset(5, 5, 5, 5);

            EditorGUILayout.LabelField("Content:", EditorStyles.boldLabel);
            contentProp.stringValue = EditorGUILayout.TextArea(contentProp.stringValue, contentStyle, GUILayout.MinHeight(60));

            EditorGUILayout.Space(10);

            // Visual options in a foldout
            EditorGUILayout.LabelField("Visual Options", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(showGizmoProp, new GUIContent("Show Gizmo"));

            if (showGizmoProp.boolValue)
            {
                EditorGUILayout.PropertyField(gizmoColorProp, new GUIContent("Gizmo Color"));
                EditorGUILayout.PropertyField(gizmoSizeProp, new GUIContent("Gizmo Size"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            // Info box
            EditorGUILayout.HelpBox("This component is editor-only and won't affect runtime performance.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}