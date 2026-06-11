using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for QuickInventoryUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes validation warnings, live hotbar tracking, and outline testers.
    /// </summary>
    [CustomEditor(typeof(QuickInventoryUI))]
    public class QuickInventoryUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Hotbar Inventory Panel HUD";

        // Serialized properties
        private SerializedProperty inventoryModePanelProp;
        private SerializedProperty inventoryModeSlotHolderProp;
        private SerializedProperty gameplayModePanelProp;
        private SerializedProperty gameplayModeSlotHolderProp;
        private SerializedProperty quickSlotPrefabProp;

        private void OnEnable()
        {
            inventoryModePanelProp = serializedObject.FindProperty("inventoryModePanel");
            inventoryModeSlotHolderProp = serializedObject.FindProperty("inventoryModeSlotHolder");
            gameplayModePanelProp = serializedObject.FindProperty("gameplayModePanel");
            gameplayModeSlotHolderProp = serializedObject.FindProperty("gameplayModeSlotHolder");
            quickSlotPrefabProp = serializedObject.FindProperty("quickSlotPrefab");
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: Inventory-Mode View ──
            BeginCard("Inventory Screen View");
            {
                DrawProperty(inventoryModePanelProp, "Inventory Panel Root", "The hotbar panel active when the main inventory overlay is open (supports active item drag & drop slots).");
                DrawProperty(inventoryModeSlotHolderProp, "Inventory Slots Grid", "Transform container where active-mode quick slot prefabs are spawned.");
            }
            EndCard();

            // ── Card 2: Gameplay-Mode View ──
            BeginCard("Gameplay Screen View");
            {
                DrawProperty(gameplayModePanelProp, "Gameplay Panel Root", "The hotbar panel active during normal gameplay HUD (supports selection-only overlays).");
                DrawProperty(gameplayModeSlotHolderProp, "Gameplay Slots Grid", "Transform container where gameplay-mode quick slot prefabs are spawned.");
            }
            EndCard();

            // ── Card 3: Slot Prefab Setup ──
            BeginCard("Slot Prefab Setup");
            {
                DrawProperty(quickSlotPrefabProp, "Hotbar Slot Prefab", "QuickSlot prefab instantiated for each hotbar block.");
                if (quickSlotPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("QuickSlot Prefab is unassigned! Hotbar slots cannot be built.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 4: Playmode Hotbar Monitor ──
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
            BeginCard("Live Hotbar HUD Debugger");
            {
                var ui = (QuickInventoryUI)target;

                // Live state details
                GUILayout.Label($"<b>HUD Inventory Mode</b>: {(ui.IsInventoryOpen ? "<color=#5CACEE>Inventory (Drag)</color>" : "<color=#66CD00>Gameplay (Select)</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Hotbar Slots Size</b>: {ui.QuickSlotCount} Slots", EditorStyles.miniLabel);

                var selectedIndex = ui.SelectedSlotIndex;
                GUILayout.Label($"<b>Selected Slot Index</b>: Slot <color=#9B30FF><b>{selectedIndex}</b></color>", new GUIStyle(EditorStyles.label) { richText = true });

                var selectedItem = ui.GetSelectedItem();
                string itemName = (selectedItem.itemId >= 0 && selectedItem.count > 0) ? $"Item ID: {selectedItem.itemId} (Count: {selectedItem.count})" : "Empty";
                GUILayout.Label($"<b>Active Equipped Item</b>: <color=#FF8C00>{itemName}</color>", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(10);

                // Grid of select slot buttons to test selection highlights
                GUILayout.Label("Simulate Hotbar Item Selection:", EditorStyles.boldLabel);
                
                int slotCount = ui.QuickSlotCount;
                int cols = 4;
                int rows = Mathf.CeilToInt((float)slotCount / cols);

                for (int r = 0; r < rows; r++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < cols; c++)
                    {
                        int index = r * cols + c;
                        if (index < slotCount)
                        {
                            bool isCurrent = (index == selectedIndex);
                            GUI.backgroundColor = isCurrent ? MEEditorTheme.ColorAccent : Color.white;

                            if (GUILayout.Button($"Slot {index}", GUILayout.Height(24)))
                            {
                                ui.SelectSlot(index);
                                Debug.Log($"[QuickInventoryUIEditor] Forcibly selected Hotbar Slot: {index}");
                            }
                        }
                        else
                        {
                            GUILayout.Space(EditorGUIUtility.fieldWidth);
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }
                GUI.backgroundColor = Color.white;

                // Request scene views refresh dynamically in Play Mode
                Repaint();
            }
            EndCard();
        }
    }
}
