using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Global Theme and Styling system for Multiplayer Engine Editor tools.
    /// Provides consistent colors, custom GUIStyles, and standard layout widgets.
    /// </summary>
    public static class MEEditorTheme
    {
        // =========================================================================
        // COLOR PALETTE (Tokens)
        // =========================================================================
        public static readonly Color ColorWindowBg = new Color(0.12f, 0.14f, 0.17f);        // Deep Slate Dark
        public static readonly Color ColorCardBg = new Color(0.16f, 0.18f, 0.22f);          // Card container dark gray
        public static readonly Color ColorAccent = new Color(0.33f, 0.41f, 0.92f);          // Royal Multiplayer Indigo/Blue
        public static readonly Color ColorAccentHover = new Color(0.42f, 0.50f, 0.96f);     // Lighter Accent Hover
        public static readonly Color ColorBorder = new Color(0.24f, 0.27f, 0.33f);          // Divider border
        public static readonly Color ColorSuccess = new Color(0.15f, 0.60f, 0.35f);         // Status Success Green
        public static readonly Color ColorWarning = new Color(0.85f, 0.55f, 0.15f);         // Status Warning Amber

        public static readonly Color ColorTextNormal = new Color(0.92f, 0.94f, 0.98f);       // Crisp off-white
        public static readonly Color ColorTextMuted = new Color(0.62f, 0.66f, 0.75f);        // Slate gray

        // =========================================================================
        // CACHED GUISTYLES
        // =========================================================================
        private static bool isInitialized = false;

        public static GUIStyle StyleWindowBg { get; private set; }
        public static GUIStyle StyleCard { get; private set; }
        public static GUIStyle StyleTitle { get; private set; }
        public static GUIStyle StyleHeaderSub { get; private set; }
        public static GUIStyle StyleDescription { get; private set; }
        public static GUIStyle StyleLabelMuted { get; private set; }
        public static GUIStyle StylePrimaryButton { get; private set; }
        public static GUIStyle StyleSecondaryButton { get; private set; }
        public static GUIStyle StyleListItem { get; private set; }
        public static GUIStyle StyleListItemSelected { get; private set; }
        public static GUIStyle StyleHeaderBox { get; private set; }
        public static GUIStyle StyleDynamicButton { get; private set; }

        private static Dictionary<Color, Texture2D> textureCache = new Dictionary<Color, Texture2D>();

        /// <summary>
        /// Initializes the GUIStyles using the current Editor Skin.
        /// Must be called inside OnGUI.
        /// </summary>
        public static void InitStyles()
        {
            if (isInitialized) return;

            // Window Background style
            StyleWindowBg = new GUIStyle();
            StyleWindowBg.normal.background = GetTexture(ColorWindowBg);

            // Card Style
            StyleCard = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 20, 20),
                margin = new RectOffset(15, 15, 10, 10),
                border = new RectOffset(4, 4, 4, 4)
            };
            StyleCard.normal.background = GetTexture(ColorCardBg);

            // Header Banner box
            StyleHeaderBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 15, 15),
                margin = new RectOffset(0, 0, 0, 10),
                border = new RectOffset(0, 0, 0, 2)
            };
            StyleHeaderBox.normal.background = GetTexture(ColorCardBg);

            // Title Typography
            StyleTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            StyleTitle.normal.textColor = ColorTextNormal;

            // Header Subtext
            StyleHeaderSub = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            StyleHeaderSub.normal.textColor = ColorTextMuted;

            // Description / WordWrapped Label
            StyleDescription = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            StyleDescription.normal.textColor = ColorTextNormal;

            // Muted Labels
            StyleLabelMuted = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11
            };
            StyleLabelMuted.normal.textColor = ColorTextMuted;

            // Primary Indigo Button
            StylePrimaryButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36,
                alignment = TextAnchor.MiddleCenter
            };
            StylePrimaryButton.normal.background = GetTexture(ColorAccent);
            StylePrimaryButton.normal.textColor = Color.white;
            StylePrimaryButton.hover.background = GetTexture(ColorAccentHover);
            StylePrimaryButton.hover.textColor = Color.white;
            StylePrimaryButton.active.background = GetTexture(ColorAccent * 0.8f);
            StylePrimaryButton.active.textColor = Color.white;

            // Secondary Dark Button
            StyleSecondaryButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36,
                alignment = TextAnchor.MiddleCenter
            };
            StyleSecondaryButton.normal.background = GetTexture(ColorWindowBg);
            StyleSecondaryButton.normal.textColor = ColorTextNormal;
            StyleSecondaryButton.hover.background = GetTexture(ColorBorder);
            StyleSecondaryButton.hover.textColor = Color.white;
            StyleSecondaryButton.active.background = GetTexture(ColorWindowBg * 0.9f);
            StyleSecondaryButton.active.textColor = Color.white;

            // List item style (Unselected)
            StyleListItem = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 2, 2)
            };

            // List item style (Selected)
            StyleListItemSelected = new GUIStyle(StyleListItem);
            StyleListItemSelected.normal.background = GetTexture(ColorAccent * 0.25f);
            StyleListItemSelected.normal.textColor = Color.white;

            // Dynamic Active Button (solid white background, designed to be tinted using GUI.backgroundColor)
            StyleDynamicButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            StyleDynamicButton.normal.background = GetTexture(Color.white);
            StyleDynamicButton.normal.textColor = Color.white;
            StyleDynamicButton.hover.background = GetTexture(Color.white);
            StyleDynamicButton.hover.textColor = Color.white;
            StyleDynamicButton.active.background = GetTexture(Color.white);
            StyleDynamicButton.active.textColor = Color.white;

            isInitialized = true;
        }

        // =========================================================================
        // REUSABLE DRAWING OPERATIONS
        // =========================================================================

        /// <summary>
        /// Draws a standardized header banner for editor windows.
        /// </summary>
        public static void DrawHeader(string title, string subtitle, Texture2D logo = null)
        {
            InitStyles();

            GUILayout.BeginVertical(StyleHeaderBox, GUILayout.ExpandWidth(true));
            
            if (logo != null)
            {
                Rect cardRect = GUILayoutUtility.GetAspectRect((float)logo.width / logo.height, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(cardRect, logo, ScaleMode.ScaleAndCrop, true);
                GUILayout.Space(12);
            }

            GUILayout.Label(title, StyleTitle, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(subtitle))
            {
                GUILayout.Space(4);
                GUILayout.Label(subtitle, StyleHeaderSub, GUILayout.ExpandWidth(true));
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a sleek divider line.
        /// </summary>
        public static void DrawDivider(float height = 1f)
        {
            InitStyles();
            Rect rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Color oldColor = GUI.color;
                GUI.color = ColorBorder;
                GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
                GUI.color = oldColor;
            }
        }

        /// <summary>
        /// Begins a beautiful container card with an optional section title.
        /// </summary>
        public static void BeginCard(string title = "")
        {
            InitStyles();
            GUILayout.BeginVertical(StyleCard, GUILayout.ExpandWidth(true));
            
            if (!string.IsNullOrEmpty(title))
            {
                GUIStyle cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    margin = new RectOffset(0, 0, 0, 10)
                };
                cardTitleStyle.normal.textColor = ColorTextNormal;
                
                GUILayout.Label(title, cardTitleStyle);
                DrawDivider();
                GUILayout.Space(12);
            }
        }

        /// <summary>
        /// Ends the container card.
        /// </summary>
        public static void EndCard()
        {
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a premium branding footer with quick links to Support and Docs.
        /// </summary>
        public static void DrawFooter(string version = "1.0.0")
        {
            InitStyles();
            GUILayout.Space(15);
            DrawDivider();
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Survival | COOP Multiplayer - Pro Edition v{version}", StyleLabelMuted);
            GUILayout.FlexibleSpace();

            GUIStyle linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                fontSize = 11,
                padding = new RectOffset(5, 5, 0, 0)
            };
            linkStyle.normal.textColor = ColorAccent;

            if (GUILayout.Button("Documentation", linkStyle))
                Application.OpenURL("https://ignitivelabs.net/Assets/MultiplayerEngineProDoc");
            
            GUILayout.Label("|", StyleLabelMuted);

            if (GUILayout.Button("Discord Support", linkStyle))
                Application.OpenURL("https://discord.gg/59cFVYavpd");

            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        // =========================================================================
        // TEXTURE HELPERS (Flyweight / Cache Pattern)
        // =========================================================================
        public static Texture2D GetTexture(Color color)
        {
            if (textureCache.TryGetValue(color, out Texture2D tex) && tex != null)
            {
                return tex;
            }

            tex = MakeTex(2, 2, color);
            textureCache[color] = tex;
            return tex;
        }

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
