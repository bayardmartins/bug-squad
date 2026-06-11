using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Base Editor Window class for all Multiplayer Engine custom windows.
    /// Manages window background, scroll-view wrapping, theme styling, and standardized rendering hooks.
    /// </summary>
    public abstract class MEEditorWindow : EditorWindow
    {
        protected Vector2 scrollPosition;

        /// <summary>
        /// Controls whether the window automatically wraps DrawBody() in a ScrollView.
        /// Override this to return false for custom layouts (like dual-pane windows).
        /// </summary>
        protected virtual bool UseGlobalScrollView => true;

        /// <summary>
        /// Subtitle text displayed in the header banner.
        /// </summary>
        protected virtual string WindowSubtitle => "Multiplayer Engine Tool";

        /// <summary>
        /// Path or resource name of the logo/banner image. Override to return a valid path if needed.
        /// </summary>
        protected virtual string HeaderLogoPath => "";

        protected virtual void OnGUI()
        {
            // 1. Initialize styles and fetch texture
            MEEditorTheme.InitStyles();

            // 2. Draw consistent deep slate dark background across the entire window
            Rect bgRect = new Rect(0, 0, position.width, position.height);
            GUI.DrawTexture(bgRect, MEEditorTheme.GetTexture(MEEditorTheme.ColorWindowBg));

            // 3. Draw standard Header banner
            DrawHeaderBanner();

            // 4. Draw ScrollView if enabled
            if (UseGlobalScrollView)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            }

            // 5. Custom body drawn by subclasses
            DrawBody();

            if (UseGlobalScrollView)
            {
                EditorGUILayout.EndScrollView();
            }

            // 6. Draw standard Footer
            DrawFooterBranding();
        }

        /// <summary>
        /// Draws the header banner. Can be overridden for custom headers.
        /// </summary>
        protected virtual void DrawHeaderBanner()
        {
            Texture2D logo = null;
            if (!string.IsNullOrEmpty(HeaderLogoPath))
            {
                logo = (Texture2D)EditorGUIUtility.Load(HeaderLogoPath);
            }
            
            // If the window has titleContent, use it as the main title
            string mainTitle = titleContent != null ? titleContent.text : name;
            MEEditorTheme.DrawHeader(mainTitle, WindowSubtitle, logo);
        }

        /// <summary>
        /// Custom rendering logic for subclasses.
        /// </summary>
        protected abstract void DrawBody();

        /// <summary>
        /// Draws the standard footer branding. Can be overridden if needed.
        /// </summary>
        protected virtual void DrawFooterBranding()
        {
            MEEditorTheme.DrawFooter();
        }
    }
}
