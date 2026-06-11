using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for InventoryUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes validation warnings and playmode toggles.
    /// </summary>
    [CustomEditor(typeof(InventoryUI))]
    public class InventoryUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Full Inventory Panel HUD";

        // Serialized properties
        private SerializedProperty inventoryPanelProp;
        private SerializedProperty slotHolderProp;
        private SerializedProperty slotPrefabProp;
        private SerializedProperty backButtonProp;

        private void OnEnable()
        {
            inventoryPanelProp = serializedObject.FindProperty("inventoryPanel");
            slotHolderProp = serializedObject.FindProperty("slotHolder");
            slotPrefabProp = serializedObject.FindProperty("slotPrefab");
            backButtonProp = serializedObject.FindProperty("backButton");
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: UI Panel Configuration ──
            BeginCard("UI Panel Layout");
            {
                DrawProperty(inventoryPanelProp, "Inventory Panel Root", "The main visual container that active/deactivates when toggling the inventory screen.");
                DrawProperty(slotHolderProp, "Slot Grid Holder", "Transform grid container where individual inventory item slot UI prefabs are generated.");
                
                if (inventoryPanelProp.objectReferenceValue == null)
                {
                    DrawMessage("Inventory Panel Root is unassigned! Visibility settings will fall back to using the root CanvasGroup.", MessageType.Info);
                }
            }
            EndCard();

            // ── Card 2: Slot & Control Prefabs ──
            BeginCard("Prefabs & Actions Setup");
            {
                DrawProperty(slotPrefabProp, "Inventory Slot Prefab", "Row slot card prefab instantiated to hold player inventory item stacks.");
                if (slotPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("Inventory Slot Prefab is unassigned! Inventory slots cannot be built.", MessageType.Error);
                }

                DrawProperty(backButtonProp, "HUD Exit Button", "HUD button overlay used to exit the inventory screen layout.");
            }
            EndCard();

            // ── Card 3: Playmode Live Debugger ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        /// <summary>
        /// Renders live controls and info from the active instance in Play Mode.
        /// </summary>
        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Inventory UI Debugger");
            {
                var ui = (InventoryUI)target;

                // Live state details
                GUILayout.Label($"<b>Inventory Open</b>: {(ui.IsInventoryOpen ? "<color=#66CD00>OPEN</color>" : "<color=#CD2626>CLOSED</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                var manager = ui.InventoryManager;
                GUILayout.Label($"<b>Associated Player Inventory</b>: {(manager != null ? $"<color=#5CACEE>{(manager as MonoBehaviour).name}</color>" : "<i>None (Uninitialized)</i>")}", new GUIStyle(EditorStyles.label) { richText = true });

                if (manager != null)
                {
                    int occupiedCount = 0;
                    if (manager.Slots != null)
                    {
                        foreach (var item in manager.Slots)
                        {
                            if (!item.IsEmpty) occupiedCount++;
                        }
                    }
                    GUILayout.Label($"<b>Items Filled</b>: {occupiedCount} / {manager.MaxInventorySize} Slots", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }

                GUILayout.Space(10);

                // Live layout triggers
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Toggle HUD", MEEditorTheme.StylePrimaryButton))
                    {
                        ui.ToggleInventory();
                        Debug.Log("[InventoryUIEditor] Triggered Inventory Toggle.");
                    }

                    if (GUILayout.Button("Force Open", MEEditorTheme.StyleSecondaryButton))
                    {
                        ui.OpenInventory();
                        Debug.Log("[InventoryUIEditor] Forcibly opened Inventory panel.");
                    }

                    if (GUILayout.Button("Force Close", MEEditorTheme.StyleSecondaryButton))
                    {
                        ui.CloseInventory();
                        Debug.Log("[InventoryUIEditor] Forcibly closed Inventory panel.");
                    }
                }
                GUILayout.EndHorizontal();

                // Request scene views refresh dynamically in Play Mode
                Repaint();
            }
            EndCard();
        }
    }
}
