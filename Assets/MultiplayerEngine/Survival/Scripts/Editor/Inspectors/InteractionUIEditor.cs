using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for InteractionUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes real-time testing buttons and custom icon previews.
    /// </summary>
    [CustomEditor(typeof(InteractionUI))]
    public class InteractionUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Interaction UI";

        // Serialized properties
        private SerializedProperty keyIconProp;
        private SerializedProperty itemIconProp;
        private SerializedProperty actionTextProp;
        private SerializedProperty nameTextProp;
        private SerializedProperty descriptionTextProp;
        private SerializedProperty keyboardIconProp;
        private SerializedProperty gamepadIconProp;
        private SerializedProperty fadeDurationProp;
        private SerializedProperty canvasGroupProp;

        // Simulator state for Playmode Debugger
        private InteractionType testType = InteractionType.Interact;
        private string testObjectName = "Survival Chest";
        private string testDescription = "Open storage container";
        private string testInfo = "[E]";
        private Sprite testIcon;

        protected virtual void OnEnable()
        {
            keyIconProp = serializedObject.FindProperty("keyIcon");
            itemIconProp = serializedObject.FindProperty("itemIcon");
            actionTextProp = serializedObject.FindProperty("actionText");
            nameTextProp = serializedObject.FindProperty("nameText");
            descriptionTextProp = serializedObject.FindProperty("descriptionText");
            keyboardIconProp = serializedObject.FindProperty("keyboardIcon");
            gamepadIconProp = serializedObject.FindProperty("gamepadIcon");
            fadeDurationProp = serializedObject.FindProperty("fadeDuration");
            canvasGroupProp = serializedObject.FindProperty("canvasGroup");
        }

        protected override void DrawInspectorBody()
        {
            // Info banner explaining component role
            DrawMessage("Attached to the canvas overlay to display interactive UI prompt cues.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Text UI Components ──
            BeginCard("Prompt Text Configurations");
            {
                DrawProperty(actionTextProp, "Action Label Text", "TMP Text field displaying the verb e.g. Pickup, Open, Use.");
                DrawProperty(nameTextProp, "Object Title Text", "TMP Text field displaying target entity name.");
                DrawProperty(descriptionTextProp, "Details Description", "TMP Text field displaying instructions or details.");

                if (actionTextProp.objectReferenceValue == null || nameTextProp.objectReferenceValue == null)
                {
                    DrawMessage("Essential TMP text components are unassigned! Prompts may not render correctly.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 2: Prompt Icon Renderers ──
            BeginCard("UI Icon Containers");
            {
                DrawProperty(keyIconProp, "Interaction Key Image", "Image UI component displaying the bound hotkey sprite.");
                DrawProperty(itemIconProp, "Target Item Image", "Optional Image UI component showing the item's customized inventory icon.");

                // Render status of assigned UI Images
                var keyImg = keyIconProp.objectReferenceValue as Image;
                var itemImg = itemIconProp.objectReferenceValue as Image;

                if (keyImg != null || itemImg != null)
                {
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                        if (keyImg != null)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Label("Key Image Status:", EditorStyles.miniLabel);
                            EditorGUILayout.HelpBox(keyImg.gameObject.activeSelf ? "Active" : "Inactive", MessageType.None);
                            GUILayout.EndVertical();
                        }
                        if (itemImg != null)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Label("Item Image Status:", EditorStyles.miniLabel);
                            EditorGUILayout.HelpBox(itemImg.gameObject.activeSelf ? "Active" : "Inactive", MessageType.None);
                            GUILayout.EndVertical();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            EndCard();

            // ── Card 3: Key Sprites Settings ──
            BeginCard("Input Device Icon Sprites");
            {
                DrawProperty(keyboardIconProp, "Keyboard/Mouse Icon", "Sprite to render when using standard PC keyboard binds.");
                DrawSpritePreview(keyboardIconProp.objectReferenceValue as Sprite);

                DrawProperty(gamepadIconProp, "Gamepad Controller Icon", "Sprite to render when utilizing dynamic controller inputs.");
                DrawSpritePreview(gamepadIconProp.objectReferenceValue as Sprite);
            }
            EndCard();

            // ── Card 4: Fading & Transition Configuration ──
            BeginCard("Fading & Transitions");
            {
                DrawProperty(fadeDurationProp, "Fade Speed (Secs)", "Time to smoothly transition the overlay transparency.");
                DrawProperty(canvasGroupProp, "Canvas Group Block", "The target CanvasGroup that holds this overlay container.");

                if (canvasGroupProp.objectReferenceValue == null)
                {
                    DrawMessage("Canvas Group is missing! Fading animation requires a valid Canvas Group.", MessageType.Error);
                }
            }
            EndCard();

            // ── Playmode Live Debugger Simulator ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        private void DrawSpritePreview(Sprite sprite)
        {
            if (sprite == null) return;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = MEEditorTheme.ColorWindowBg;
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(36), GUILayout.Height(36));
                {
                    Rect rect = GUILayoutUtility.GetRect(28, 28);
                    DrawSprite(rect, sprite);
                }
                GUILayout.EndVertical();
                GUI.backgroundColor = oldBg;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Prompt UI Debugger");
            {
                var ui = (InteractionUI)target;

                // Live status display
                GUILayout.Label($"<b>UI Visibility State</b>: {(ui.IsVisible ? "<color=#66CD00>SHOWN</color>" : "<color=#CD2626>HIDDEN</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                // Simulation controls header
                GUILayout.Label("<b>Live Prompt Simulator</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                GUILayout.Label("Test overlay layouts and typography limits directly in real-time.", EditorStyles.miniLabel);
                GUILayout.Space(4);

                testType = (InteractionType)EditorGUILayout.EnumPopup("Prompt Action Type", testType);
                testObjectName = EditorGUILayout.TextField("Target Object Name", testObjectName);
                testDescription = EditorGUILayout.TextField("Detail Sub-Description", testDescription);
                testInfo = EditorGUILayout.TextField("Extra Info Text", testInfo);
                testIcon = (Sprite)EditorGUILayout.ObjectField("Item Overlay Sprite", testIcon, typeof(Sprite), false);

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Simulate Show Prompt", MEEditorTheme.StylePrimaryButton))
                    {
                        ui.Show(testType, testObjectName, testDescription, testInfo, testIcon);
                        Debug.Log($"[InteractionUIEditor] Simulated Show Prompt: {testType} - {testObjectName}");
                    }

                    if (GUILayout.Button("Simulate Hide UI", MEEditorTheme.StyleSecondaryButton))
                    {
                        ui.Hide();
                        Debug.Log("[InteractionUIEditor] Simulated Hide UI.");
                    }
                }
                GUILayout.EndHorizontal();

                Repaint();
            }
            EndCard();
        }

        // Helper to draw sprites perfectly inside GUI Rects
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