using UnityEditor;
using UnityEngine;

namespace UnityProductivityTools.Dashboard
{
    public static class DashboardStyle
    {
        // Colors
        public static readonly Color HeaderColor = new Color(0.12f, 0.12f, 0.12f);
        public static readonly Color SidebarColor = new Color(0.15f, 0.15f, 0.15f);
        public static readonly Color AccentColor = new Color(0f, 0.8f, 0.8f); // Neon Cyan
        public static readonly Color HoverColor = new Color(0.25f, 0.25f, 0.25f);
        public static readonly Color BorderColor = new Color(0.2f, 0.2f, 0.2f);
        public static readonly Color TextDim = new Color(0.7f, 0.7f, 0.7f);

        // Styles
        private static GUIStyle _sidebarButton;
        public static GUIStyle SidebarButton
        {
            get
            {
                if (_sidebarButton == null)
                {
                    _sidebarButton = new GUIStyle(GUI.skin.button);
                    _sidebarButton.normal.background = CreateTexture(2, 2, SidebarColor);
                    _sidebarButton.hover.background = CreateTexture(2, 2, HoverColor);
                    _sidebarButton.active.background = CreateTexture(2, 2, AccentColor);
                    _sidebarButton.normal.textColor = TextDim;
                    _sidebarButton.hover.textColor = Color.white;
                    _sidebarButton.active.textColor = Color.black;
                    _sidebarButton.alignment = TextAnchor.MiddleLeft;
                    _sidebarButton.padding = new RectOffset(15, 10, 8, 8);
                    _sidebarButton.margin = new RectOffset(0, 0, 0, 0);
                    _sidebarButton.fixedHeight = 40;
                    _sidebarButton.fontSize = 12;
                    _sidebarButton.fontStyle = FontStyle.Normal;
                    _sidebarButton.border = new RectOffset(0, 0, 0, 0);
                }
                return _sidebarButton;
            }
        }

        private static GUIStyle _sidebarButtonSelected;
        public static GUIStyle SidebarButtonSelected
        {
            get
            {
                if (_sidebarButtonSelected == null)
                {
                    _sidebarButtonSelected = new GUIStyle(SidebarButton);
                    _sidebarButtonSelected.normal.background = CreateTexture(2, 2, new Color(0.2f, 0.2f, 0.2f));
                    _sidebarButtonSelected.normal.textColor = AccentColor;
                    _sidebarButtonSelected.fontStyle = FontStyle.Bold;
                }
                return _sidebarButtonSelected;
            }
        }

        private static GUIStyle _headerLabel;
        public static GUIStyle HeaderLabel
        {
            get
            {
                if (_headerLabel == null)
                {
                    _headerLabel = new GUIStyle(EditorStyles.boldLabel);
                    _headerLabel.fontSize = 18;
                    _headerLabel.normal.textColor = Color.white;
                    _headerLabel.margin = new RectOffset(10, 10, 20, 10);
                }
                return _headerLabel;
            }
        }

        private static GUIStyle _sectionHeader;
        public static GUIStyle SectionHeader
        {
            get
            {
                if (_sectionHeader == null)
                {
                    _sectionHeader = new GUIStyle(EditorStyles.boldLabel);
                    _sectionHeader.fontSize = 14;
                    _sectionHeader.normal.textColor = AccentColor;
                    _sectionHeader.margin = new RectOffset(0, 0, 15, 5);
                }
                return _sectionHeader;
            }
        }

        private static GUIStyle _cardStyle;
        public static GUIStyle CardStyle
        {
            get
            {
                if (_cardStyle == null)
                {
                    _cardStyle = new GUIStyle(EditorStyles.helpBox);
                    _cardStyle.padding = new RectOffset(15, 15, 15, 15);
                    _cardStyle.margin = new RectOffset(5, 5, 5, 5);
                    _cardStyle.normal.background = CreateTexture(2, 2, new Color(0.18f, 0.18f, 0.18f));
                }
                return _cardStyle;
            }
        }

        private static Texture2D CreateTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = color;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public static void DrawLine(float thickness = 1)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness);
            rect.height = thickness;
            EditorGUI.DrawRect(rect, BorderColor);
        }
    }
}
