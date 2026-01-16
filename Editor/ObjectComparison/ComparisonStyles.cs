using UnityEditor;
using UnityEngine;

namespace UnityTools.ObjectComparison
{
    public static class ComparisonStyles
    {
        public static GUIStyle HeaderStyle;
        public static GUIStyle RowBackground;
        
        public static Color ColorIdentical = new Color(0.1f, 0.8f, 0.1f, 0.2f);
        public static Color ColorModified = new Color(1f, 0.6f, 0, 0.2f);
        public static Color ColorMissing = new Color(1f, 0.2f, 0.2f, 0.2f); // Red
        public static Color ColorAdded = new Color(0.2f, 0.6f, 1f, 0.2f);   // Blue

        public static Color TextGreen = new Color(0.2f, 0.8f, 0.2f);
        public static Color TextOrange = new Color(1f, 0.6f, 0f);
        public static Color TextRed = new Color(1f, 0.3f, 0.3f);
        public static Color TextBlue = new Color(0.3f, 0.7f, 1f);

        static ComparisonStyles()
        {
            HeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            HeaderStyle.fontSize = 12;
            HeaderStyle.alignment = TextAnchor.MiddleCenter;

            RowBackground = new GUIStyle(GUI.skin.box);
            RowBackground.padding = new RectOffset(5, 5, 5, 5);
            RowBackground.margin = new RectOffset(0, 0, 0, 0);
        }

        public static GUIStyle GetStyleForDiff(DiffType diff)
        {
            // Simple helper if needed
            return EditorStyles.label;
        }

        public static Color GetColorForDiff(DiffType diff)
        {
            switch (diff)
            {
                case DiffType.Identical: return ColorIdentical;
                case DiffType.Modified: return ColorModified;
                case DiffType.Missing: return ColorMissing;
                case DiffType.Added: return ColorAdded;
                default: return Color.clear;
            }
        }
    }
}
