using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for MarkerTarget to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// </summary>
    [CustomEditor(typeof(MarkerTarget))]
    [CanEditMultipleObjects]
    public class MarkerTargetEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Point of Interest / Target Marker";

        // Serialized properties
        private SerializedProperty iconProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty markerColorProp;
        private SerializedProperty heightOffsetProp;
        private SerializedProperty showByDefaultProp;

        private void OnEnable()
        {
            iconProp = serializedObject.FindProperty("icon");
            displayNameProp = serializedObject.FindProperty("displayName");
            markerColorProp = serializedObject.FindProperty("markerColor");
            heightOffsetProp = serializedObject.FindProperty("heightOffset");
            showByDefaultProp = serializedObject.FindProperty("showByDefault");
        }

        protected override void DrawInspectorBody()
        {
            var sprite = iconProp.objectReferenceValue as Sprite;

            // ── Card 1: Setup and Settings ──
            BeginCard("Marker Settings");
            {
                // Display Name
                DrawProperty(displayNameProp, "Display Name", "The name displayed above the icon at close range.");
                
                // Icon Picker
                DrawProperty(iconProp, "Icon Sprite", "The icon shown in the UI (falls back to default ping icon if none assigned).");

                // Icon Preview if present
                if (sprite != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                        
                        // Render a beautiful miniature icon preview
                        Color cachedBg = GUI.backgroundColor;
                        GUI.backgroundColor = MEEditorTheme.ColorWindowBg;
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(48), GUILayout.Height(48));
                        {
                            Rect rect = GUILayoutUtility.GetRect(40, 40);
                            Color cachedColor = GUI.color;
                            GUI.color = markerColorProp.colorValue; // Tint preview icon with selected color!
                            DrawSprite(rect, sprite);
                            GUI.color = cachedColor;
                        }
                        GUILayout.EndVertical();
                        GUI.backgroundColor = cachedBg;
                        
                        GUILayout.BeginVertical();
                        {
                            GUILayout.Space(8);
                            GUIStyle previewTextStyle = new GUIStyle(EditorStyles.miniLabel);
                            previewTextStyle.normal.textColor = MEEditorTheme.ColorTextMuted;
                            GUILayout.Label("Sprite Preview (Tinted)", previewTextStyle);
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(6);
                }

                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(8);

                // Tint Color
                DrawProperty(markerColorProp, "Marker Tint", "Color applied to the icon, text, and distance label.");

                // Height Offset (Slider)
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(new GUIContent("Height Offset", "Vertical offset in world units above the object's pivot."), GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                    EditorGUILayout.PropertyField(heightOffsetProp, GUIContent.none);
                    GUILayout.Label("m", GUILayout.Width(15));
                }
                EditorGUILayout.EndHorizontal();

                // Show by Default
                DrawProperty(showByDefaultProp, "Show By Default", "If true, registers automatically when enabled. If false, must be registered manually via script.");
            }
            EndCard();

            // ── Card 2: Visual Mockup / Premium HUD Preview ──
            BeginCard("HUD UI Preview");
            {
                GUIStyle previewBoxStyle = new GUIStyle(GUI.skin.box);
                previewBoxStyle.normal.background = MEEditorTheme.GetTexture(new Color(0.08f, 0.09f, 0.11f));
                previewBoxStyle.padding = new RectOffset(16, 16, 16, 16);

                GUILayout.BeginVertical(previewBoxStyle);
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    {
                        // Mock UI Marker Render
                        Color cachedColor = GUI.color;
                        GUI.color = markerColorProp.colorValue;

                        // Draw Icon Mockup
                        if (sprite != null)
                        {
                            Rect iconRect = GUILayoutUtility.GetRect(32, 32);
                            DrawSprite(iconRect, sprite);
                        }
                        else
                        {
                            // Placeholder circle/square
                            Rect placeholderRect = GUILayoutUtility.GetRect(32, 32);
                            GUI.DrawTexture(placeholderRect, EditorGUIUtility.whiteTexture);
                        }

                        GUILayout.Space(4);

                        // Draw Text Mockup
                        GUIStyle mockNameStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 11
                        };
                        mockNameStyle.normal.textColor = markerColorProp.colorValue;

                        string nameText = string.IsNullOrEmpty(displayNameProp.stringValue) ? "Target" : displayNameProp.stringValue;
                        GUILayout.Label(nameText, mockNameStyle, GUILayout.Width(80));

                        GUI.color = cachedColor;
                    }
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                hintStyle.normal.textColor = MEEditorTheme.ColorTextMuted;
                GUILayout.Label("Sleek mockup of the 3D HUD billboard element in-game.", hintStyle);
            }
            EndCard();
        }

        // Helper to draw sprites perfectly inside GUI Rects (handles atlas margins and non-square textures)
        private void DrawSprite(Rect position, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.textureRect;

            Rect texCoords = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = position.width / position.height;

            Rect drawRect = position;
            if (spriteAspect > rectAspect)
            {
                float newHeight = position.width / spriteAspect;
                drawRect.y += (position.height - newHeight) / 2f;
                drawRect.height = newHeight;
            }
            else
            {
                float newWidth = position.height * spriteAspect;
                drawRect.x += (position.width - newWidth) / 2f;
                drawRect.width = newWidth;
            }

            GUI.DrawTextureWithTexCoords(drawRect, tex, texCoords);
        }
    }
}