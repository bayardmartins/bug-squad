using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for EquipmentInfoUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// </summary>
    [CustomEditor(typeof(EquipmentInfoUI))]
    public class EquipmentInfoUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Equipped Item HUD Overlay";

        // Serialized properties
        private SerializedProperty itemHolderProp;
        private SerializedProperty itemIconProp;
        private SerializedProperty itemCountTextProp;
        private SerializedProperty ammoHolderProp;
        private SerializedProperty currentAmmoTextProp;
        private SerializedProperty totalAmmoTextProp;
        private SerializedProperty ammoIconProp;
        private SerializedProperty fadeDurationProp;

        protected virtual void OnEnable()
        {
            itemHolderProp = serializedObject.FindProperty("itemHolder");
            itemIconProp = serializedObject.FindProperty("itemIcon");
            itemCountTextProp = serializedObject.FindProperty("itemCountText");
            ammoHolderProp = serializedObject.FindProperty("ammoHolder");
            currentAmmoTextProp = serializedObject.FindProperty("currentAmmoText");
            totalAmmoTextProp = serializedObject.FindProperty("totalAmmoText");
            ammoIconProp = serializedObject.FindProperty("ammoIcon");
            fadeDurationProp = serializedObject.FindProperty("fadeDuration");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Attached to the canvas overlay to display active equipment data and ammo reserve metrics.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: UI Root Panels ──
            BeginCard("Root Containers & Timers");
            {
                DrawProperty(itemHolderProp, "Item Holder Root", "GameObject root wrapper containing the active equipped item layout.");
                DrawProperty(fadeDurationProp, "Fading Speed (Secs)", "Time to smoothly transition the overlay transparency.");

                if (itemHolderProp.objectReferenceValue == null)
                {
                    DrawMessage("Item Holder Root is missing! Fading cannot toggle active states correctly.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 2: Main Item Display ──
            BeginCard("Equipped Item Setup");
            {
                DrawProperty(itemIconProp, "Item Icon Image", "UI Image component rendering the active equipped tool/weapon sprite.");
                DrawProperty(itemCountTextProp, "Item Quantity Text", "TMP Text field displaying stack quantities (e.g., 'x30').");

                var iconImg = itemIconProp.objectReferenceValue as Image;
                if (iconImg != null && iconImg.sprite != null)
                {
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                        Color oldBg = GUI.backgroundColor;
                        GUI.backgroundColor = MEEditorTheme.ColorWindowBg;
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(32), GUILayout.Height(32));
                        {
                            Rect rect = GUILayoutUtility.GetRect(24, 24);
                            DrawSprite(rect, iconImg.sprite);
                        }
                        GUILayout.EndVertical();
                        GUI.backgroundColor = oldBg;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            EndCard();

            // ── Card 3: Ammo HUD Section ──
            BeginCard("Weapon Ammo Display");
            {
                DrawProperty(ammoHolderProp, "Ammo Holder Root", "GameObject root wrapper containing shooter/bow weapon ammo parameters.");
                DrawProperty(currentAmmoTextProp, "Current Magazine Text", "TMP Text field representing bullets loaded in magazine/chamber.");
                DrawProperty(totalAmmoTextProp, "Total Reserves Text", "TMP Text field representing total ammo remaining in inventory.");
                DrawProperty(ammoIconProp, "Ammo Icon Image", "UI Image component showing the compatible ammo sprite.");

                if (ammoHolderProp.objectReferenceValue == null)
                {
                    DrawMessage("Ammo Holder Root is missing! Ammo indicators cannot be toggled.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Playmode Live Debugger ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Equipment UI Debugger");
            {
                var ui = (EquipmentInfoUI)target;

                // Reflection search for resolved references
                var ec = ui.GetType().GetField("equipmentController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) as MonoBehaviour;
                var inv = ui.GetType().GetField("inventoryManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) as MonoBehaviour;

                GUILayout.Label($"<b>Equip Controller</b>: {(ec != null ? $"<color=#5CACEE>{ec.name}</color>" : "<i>Scanning (Uninitialized)</i>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Inventory Manager</b>: {(inv != null ? $"<color=#5CACEE>{inv.name}</color>" : "<i>Scanning (Uninitialized)</i>")}", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                var lastId = (int)(ui.GetType().GetField("lastItemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) ?? -1);
                var hasAmmo = (bool)(ui.GetType().GetField("hasAmmo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) ?? false);

                if (lastId >= 0)
                {
                    GUILayout.Label($"<b>Equipped Item ID</b>: <color=#66CD00>{lastId}</color>", new GUIStyle(EditorStyles.label) { richText = true });
                    GUILayout.Label($"<b>Requires Ammo</b>: {(hasAmmo ? "<color=#66CD00>YES</color>" : "<color=#CD2626>NO</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                }
                else
                {
                    GUILayout.Label("<b>Current State</b>: <i>No item equipped in active hand.</i>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }

                Repaint();
            }
            EndCard();
        }

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