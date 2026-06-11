using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for the Interactable class and its subclasses (excluding Pickable which has its own editor).
    /// Inherits from the universal base class MEEditorInspector.
    /// Standardized to style inspectors via MEEditorTheme with headers, cards, and validation alerts.
    /// </summary>
    [CustomEditor(typeof(Interactable), true)]
    [CanEditMultipleObjects]
    public class InteractableEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Survival Object Interaction";

        // Serialized properties
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty interactionTypeProp;
        private SerializedProperty interactionIconProp;

        protected virtual void OnEnable()
        {
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            interactionTypeProp = serializedObject.FindProperty("interactionType");
            interactionIconProp = serializedObject.FindProperty("interactionIcon");
        }

        protected override void DrawInspectorBody()
        {
            // Info banner explaining component role
            DrawMessage("Attached to game entities that players can focus on and interact with (e.g. chests, crafting tables).", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Display & Prompt Settings ──
            BeginCard("Interaction Prompt Settings");
            {
                DrawProperty(displayNameProp, "Display Name", "Prompt name shown on the HUD when looking at this object.");
                
                // Draw multiline description
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(new GUIContent("Description", "Brief details shown in HUD details mode."), GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                    descriptionProp.stringValue = EditorGUILayout.TextArea(descriptionProp.stringValue, GUILayout.Height(45));
                }
                EditorGUILayout.EndHorizontal();

                DrawProperty(interactionIconProp, "Interaction Icon", "HUD Icon displayed when this object is focused.");

                // Icon mini-preview
                var sprite = interactionIconProp.objectReferenceValue as Sprite;
                if (sprite != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                        Color oldBg = GUI.backgroundColor;
                        GUI.backgroundColor = MEEditorTheme.ColorWindowBg;
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(32), GUILayout.Height(32));
                        {
                            Rect rect = GUILayoutUtility.GetRect(24, 24);
                            DrawSprite(rect, sprite);
                        }
                        GUILayout.EndVertical();
                        GUI.backgroundColor = oldBg;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                }

                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                // Draw Interaction Type dropdown only if NOT a CraftingTable
                if (target is CraftingTable)
                {
                    // CraftingTable always uses InteractionType.Interact
                    DrawMessage("Crafting Table is locked to the 'Interact' type.", MessageType.Info);
                }
                else
                {
                    // Custom dropdown for Interactable to hide 'Pickup'
                    DrawCustomInteractionTypeDropdown();
                }
            }
            EndCard();

            // ── Card 2: Subclass Specific Properties (if any exist) ──
            DrawRemainingProperties();
        }

        private void DrawCustomInteractionTypeDropdown()
        {
            if (interactionTypeProp == null) return;

            string[] options = new string[] { "Interact", "Details" };
            
            // Map: Interact (1) -> index 0, Details (2) -> index 1, Pickup (0) -> default to index 0
            int currentIndex = 0;
            if (interactionTypeProp.intValue == 2)
            {
                currentIndex = 1;
            }
            else if (interactionTypeProp.intValue == 1)
            {
                currentIndex = 0;
            }
            else
            {
                currentIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(new GUIContent("Interaction Type", "Type of interaction prompt shown in HUD. 'Pickup' is hidden as it should be handled via Pickable components."), currentIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                // Map back: index 0 -> Interact (1), index 1 -> Details (2)
                interactionTypeProp.intValue = (newIndex == 1) ? 2 : 1;
            }
        }

        private void DrawRemainingProperties()
        {
            // Iterate and draw any extra serialized properties defined in child classes
            bool hasCustomProps = false;
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Skip standard MonoBehaviour/NetworkBehaviour properties and our custom drawn ones
                if (prop.name == "m_Script" || 
                    prop.name == "displayName" || 
                    prop.name == "description" || 
                    prop.name == "interactionType" || 
                    prop.name == "interactionIcon")
                {
                    continue;
                }

                // If we hit any custom child field, draw a specific card for them
                if (!hasCustomProps)
                {
                    BeginCard("Extra Custom Properties");
                    hasCustomProps = true;
                }

                EditorGUILayout.PropertyField(prop, true);
            }

            if (hasCustomProps)
            {
                EndCard();
            }
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