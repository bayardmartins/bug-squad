using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Inspector Editor for CharacterData ScriptableObject.
    /// Provides an extremely polished dashboard displaying character icons,
    /// dynamic progress bars for statistics (Stealth, Mobility), and clean prefab links.
    /// </summary>
    [CustomEditor(typeof(CharacterData))]
    public class CharacterDataEditor : UnityEditor.Editor
    {
        private CharacterData character;

        private SerializedProperty characterNameProp;
        private SerializedProperty characterIdProp;
        private SerializedProperty characterIconProp;
        private SerializedProperty characterPrefabProp;
        private SerializedProperty characterLobbyPrefabProp;
        private SerializedProperty stealthProp;
        private SerializedProperty mobilityProp;

        private void OnEnable()
        {
            character = (CharacterData)target;

            characterNameProp = serializedObject.FindProperty("characterName");
            characterIdProp = serializedObject.FindProperty("characterId");
            characterIconProp = serializedObject.FindProperty("characterIcon");
            characterPrefabProp = serializedObject.FindProperty("characterPrefab");
            characterLobbyPrefabProp = serializedObject.FindProperty("characterLobbyPrefab");
            stealthProp = serializedObject.FindProperty("stealth");
            mobilityProp = serializedObject.FindProperty("mobility");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Header Banner
            MEEditorTheme.DrawHeader("Playable Character Profile", "Stealth, Mobility, and Multiplayer Prefab Settings");

            GUILayout.Space(5);

            // 2. Character Info Header with Icon Preview
            MEEditorTheme.BeginCard("Identity Overview");
            {
                EditorGUILayout.BeginHorizontal();

                // Draw Avatar Icon
                Rect iconPreviewRect = EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(64), GUILayout.Height(64));
                {
                    var icon = characterIconProp.objectReferenceValue as Sprite;
                    if (icon != null)
                    {
                        Rect rect = GUILayoutUtility.GetRect(56, 56);
                        DrawSprite(rect, icon);
                    }
                    else
                    {
                        GUILayout.Label("No Icon", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(56));
                    }
                }
                EditorGUILayout.EndVertical();

                GUILayout.Space(12);

                // Identity Fields
                EditorGUILayout.BeginVertical();
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(characterNameProp, new GUIContent("Display Name", "Public name displayed in-game."));
                    EditorGUILayout.PropertyField(characterIdProp, new GUIContent("Unique ID", "Multiplayer backend identifier."));
                    EditorGUILayout.PropertyField(characterIconProp, new GUIContent("Avatar Sprite", "Profile picture shown in lobby/HUD."));

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(character);
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            MEEditorTheme.EndCard();

            // 3. Gameplay Stats Dashboard Card
            MEEditorTheme.BeginCard("Gameplay Capabilities & Attributes");
            {
                EditorGUI.BeginChangeCheck();

                // Stealth Attribute with Bar
                EditorGUILayout.LabelField("Stealth & Noise Reduction Index");
                DrawProgressBar(stealthProp.floatValue, MEEditorTheme.ColorAccent);
                EditorGUILayout.PropertyField(stealthProp, new GUIContent("Stealth Value", "Reduces step noise radius and detection rates in-game."));

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Mobility Attribute with Bar
                EditorGUILayout.LabelField("Speed & Tactical Mobility Index");
                DrawProgressBar(mobilityProp.floatValue, MEEditorTheme.ColorSuccess);
                EditorGUILayout.PropertyField(mobilityProp, new GUIContent("Mobility Value", "Affects sprinting speed, stamina recharge, and jump heights."));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(character);
                }
            }
            MEEditorTheme.EndCard();

            // 4. Multiplayer Prefab References Card
            MEEditorTheme.BeginCard("Network & Model Prefabs");
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(characterPrefabProp, new GUIContent("Gameplay Prefab", "Multiplayer-synced player prefab spawned in actual match scenes."));
                EditorGUILayout.PropertyField(characterLobbyPrefabProp, new GUIContent("Lobby Prefab", "Visual dummy prefab instantiated on the main menu lobby screen."));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(character);
                }

                // Diagnostics Check
                if (characterPrefabProp.objectReferenceValue == null || characterLobbyPrefabProp.objectReferenceValue == null)
                {
                    GUILayout.Space(8);
                    EditorGUILayout.HelpBox("Warning: Character prefabs are unassigned. Spawning in lobby or game will fail!", MessageType.Warning);
                }
            }
            MEEditorTheme.EndCard();

            // 5. Standard Footer branding
            MEEditorTheme.DrawFooter();
        }

        /// <summary>
        /// Draws a premium styled stat progress bar in the custom inspector.
        /// </summary>
        private void DrawProgressBar(float fillRatio, Color color)
        {
            Rect barRect = EditorGUILayout.GetControlRect(false, 16);
            
            // Draw background gray bar
            EditorGUI.DrawRect(barRect, new Color(0.24f, 0.27f, 0.33f));

            // Draw filled ratio
            float fillWidth = barRect.width * Mathf.Clamp01(fillRatio);
            if (fillWidth > 0)
            {
                Rect filledRect = new Rect(barRect.x, barRect.y, fillWidth, barRect.height);
                EditorGUI.DrawRect(filledRect, color);
            }

            // Draw border lines
            Color borderColor = MEEditorTheme.ColorBorder;
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), borderColor); // Top
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), borderColor); // Bottom
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, 1, barRect.height), borderColor); // Left
            EditorGUI.DrawRect(new Rect(barRect.xMax - 1, barRect.y, 1, barRect.height), borderColor); // Right

            // Draw text overlay
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(barRect, $"{(fillRatio * 100f).ToString("F0")}% Capabilities Index", labelStyle);
            
            GUILayout.Space(4);
        }

        /// <summary>
        /// Draws a sprite aspect-fitted inside the coordinate rect bounds.
        /// </summary>
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
