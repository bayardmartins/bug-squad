using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Base Custom Inspector class for all Multiplayer Engine script inspectors.
    /// Standardized to style inspectors via MEEditorTheme with headers, cards, footers, and validation alerts.
    /// </summary>
    public abstract class MEEditorInspector : UnityEditor.Editor
    {
        /// <summary>
        /// Controls whether the inspector automatically renders a branded header banner.
        /// </summary>
        protected virtual bool DrawHeader => true;

        /// <summary>
        /// Controls whether the inspector automatically renders a compact branded footer.
        /// </summary>
        protected virtual bool DrawFooter => true;

        /// <summary>
        /// Custom subtitle displayed in the header banner below the script name.
        /// </summary>
        protected virtual string InspectorSubtitle => "Multiplayer Engine Component";

        /// <summary>
        /// Custom icon content for the inspector header.
        /// </summary>
        protected virtual Texture2D HeaderIcon => null;

        public override void OnInspectorGUI()
        {
            // 1. Initialize custom styles inside MEEditorTheme
            MEEditorTheme.InitStyles();

            // 2. Draw branded Header Banner if enabled
            if (DrawHeader)
            {
                DrawBrandedHeader();
            }

            // 3. Update serialized object
            serializedObject.Update();

            // 4. Custom inspector body drawn by subclasses
            EditorGUI.BeginChangeCheck();
            DrawInspectorBody();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            // 5. Draw branded Footer if enabled
            if (DrawFooter)
            {
                DrawBrandedFooter();
            }
        }

        /// <summary>
        /// Main rendering hook for custom inspector content in subclasses.
        /// </summary>
        protected abstract void DrawInspectorBody();

        /// <summary>
        /// Renders the standardized branded inspector header.
        /// </summary>
        protected virtual void DrawBrandedHeader()
        {
            GUILayout.Space(6);
            
            // Outer header panel
            GUILayout.BeginVertical(MEEditorTheme.StyleHeaderBox, GUILayout.ExpandWidth(true));
            {
                GUILayout.BeginHorizontal();
                {
                    // Draw custom icon or standard script icon
                    Texture2D icon = HeaderIcon != null ? HeaderIcon : EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                        GUILayout.Space(6);
                    }

                    // Draw component type/class name
                    string scriptName = target.GetType().Name;
                    GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 13,
                        alignment = TextAnchor.MiddleLeft
                    };
                    titleStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
                    GUILayout.Label(scriptName, titleStyle);
                }
                GUILayout.EndHorizontal();

                // Draw subtitle
                if (!string.IsNullOrEmpty(InspectorSubtitle))
                {
                    GUILayout.Space(2);
                    GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 11
                    };
                    subtitleStyle.normal.textColor = MEEditorTheme.ColorTextMuted;
                    GUILayout.Label(InspectorSubtitle, subtitleStyle);
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        /// <summary>
        /// Renders the standardized branded inspector footer.
        /// </summary>
        protected virtual void DrawBrandedFooter()
        {
            GUILayout.Space(12);
            MEEditorTheme.DrawDivider();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            {
                GUIStyle footerTextStyle = new GUIStyle(EditorStyles.miniLabel);
                footerTextStyle.normal.textColor = MEEditorTheme.ColorTextMuted;
                GUILayout.Label("Survival | COOP Multiplayer", footerTextStyle);
                
                GUILayout.FlexibleSpace();

                GUIStyle linkStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    fontSize = 9,
                    padding = new RectOffset(4, 4, 0, 0)
                };
                linkStyle.normal.textColor = MEEditorTheme.ColorAccent;

                if (GUILayout.Button("Docs", linkStyle))
                    Application.OpenURL("https://ignitivelabs.net/Assets/MultiplayerEngineProDoc");

                GUILayout.Label("|", footerTextStyle);

                if (GUILayout.Button("Discord", linkStyle))
                    Application.OpenURL("https://discord.gg/59cFVYavpd");
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // =========================================================================
        // WIDGET & CARD DRAWING HELPERS FOR SUBCLASSES
        // =========================================================================

        /// <summary>
        /// Helper to easily wrap content in a beautiful card container.
        /// </summary>
        protected void BeginCard(string title = "")
        {
            MEEditorTheme.BeginCard(title);
        }

        /// <summary>
        /// Helper to end a card container.
        /// </summary>
        protected void EndCard()
        {
            MEEditorTheme.EndCard();
        }

        /// <summary>
        /// Renders a beautiful colored title section header.
        /// </summary>
        protected void DrawSectionHeader(string title)
        {
            GUILayout.Space(8);
            GUIStyle sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(2, 2, 2, 5)
            };
            sectionHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
            GUILayout.Label(title, sectionHeaderStyle);
            MEEditorTheme.DrawDivider();
            GUILayout.Space(4);
        }

        /// <summary>
        /// Helper to draw a standard property field inside cards.
        /// </summary>
        protected void DrawProperty(SerializedProperty property, string label = "", string tooltip = "")
        {
            if (property == null) return;
            
            GUIContent guiContent = string.IsNullOrEmpty(label) 
                ? new GUIContent(property.displayName, property.tooltip) 
                : new GUIContent(label, tooltip);
                
            EditorGUILayout.PropertyField(property, guiContent);
        }

        /// <summary>
        /// Helper to draw standard help boxes styled elegantly.
        /// </summary>
        protected void DrawMessage(string text, MessageType type)
        {
            EditorGUILayout.HelpBox(text, type);
        }
    }
}
