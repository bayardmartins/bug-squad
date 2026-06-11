using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.Reflection;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Premium custom editor for InventoryManager leveraging the universal MEEditorInspector base.
    /// Provides categorized configurations, visual warning systems, live inventory grid, and administration controls.
    /// </summary>
    [CustomEditor(typeof(InventoryManager))]
    public class InventoryManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Server-Authoritative Networked Inventory";

        private SerializedProperty itemDatabaseProp;
        private SerializedProperty maxInventorySizeProp;
        private SerializedProperty quickSlotCountProp;

        // Playmode tester properties
        private int addItemId = 0;
        private int addAmount = 1;
        private bool showInventorySlots = true;

        protected virtual void OnEnable()
        {
            itemDatabaseProp = serializedObject.FindProperty("itemDatabase");
            maxInventorySizeProp = serializedObject.FindProperty("maxInventorySize");
            quickSlotCountProp = serializedObject.FindProperty("quickSlotCount");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Server-authoritative inventory system synchronizing item stacks, quick slots, durability, and loaded ammo over Netcode.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Core Configuration ──
            BeginCard("Inventory Size & Limits");
            {
                DrawProperty(itemDatabaseProp, "Item Database", "Asset catalog referencing all items and their prefabs/icons.");
                DrawProperty(maxInventorySizeProp, "Max Inventory Slots", "Total storage capacity including quick slots.");
                DrawProperty(quickSlotCountProp, "Quick Slot Count", "Number of slots mapped to quick select (first N slots).");

                if (itemDatabaseProp.objectReferenceValue == null)
                {
                    DrawMessage("Item Database is unassigned! Players will not be able to load or collect items correctly.", MessageType.Error);
                }

                if (maxInventorySizeProp.intValue <= 0)
                {
                    DrawMessage("Max inventory size must be at least 1 slot.", MessageType.Warning);
                }

                if (quickSlotCountProp.intValue > maxInventorySizeProp.intValue)
                {
                    DrawMessage("Quick Slot Count cannot exceed the Max Inventory Size.", MessageType.Warning);
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
            var manager = (InventoryManager)target;

            // ── Card 2: Live Monitor ──
            BeginCard("Live Inventory Diagnostics");
            {
                GUILayout.Label($"<b>Network State</b>: {(manager.IsServer ? "<color=#66CD00>SERVER AUTHORITATIVE</color>" : "<color=#5CACEE>CLIENT (READ-ONLY)</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                
                // Show owner ID if possible
                var ownerField = manager.GetType().GetField("netPlayerId", BindingFlags.NonPublic | BindingFlags.Instance);
                var netOwnerId = ownerField?.GetValue(manager) as NetworkVariable<Unity.Collections.FixedString64Bytes>;
                string ownerId = netOwnerId != null ? netOwnerId.Value.ToString() : "None";
                GUILayout.Label($"<b>Owner Player ID</b>: <color=#5CACEE>{(string.IsNullOrEmpty(ownerId) ? "Unassigned" : ownerId)}</color>", new GUIStyle(EditorStyles.label) { richText = true });

                int emptySlotsCount = 0;
                if (manager.Slots != null)
                {
                    for (int i = 0; i < manager.Slots.Count; i++)
                    {
                        if (manager.Slots[i].IsEmpty) emptySlotsCount++;
                    }
                    GUILayout.Label($"<b>Storage Capacity</b>: {manager.Slots.Count - emptySlotsCount} / {manager.Slots.Count} Slots Occupied", EditorStyles.miniLabel);
                }
            }
            EndCard();

            // ── Card 3: Interactive Administration Controls (Server or Host only) ──
            BeginCard("Item Spawning & Save Controls");
            {
                if (manager.IsServer)
                {
                    GUILayout.Label("<b>Administrative Item Injector</b>", EditorStyles.miniBoldLabel);
                    
                    GUILayout.BeginHorizontal();
                    {
                        addItemId = EditorGUILayout.IntField("Item ID", addItemId);
                        addAmount = EditorGUILayout.IntField("Amount", addAmount);
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Inject Item into Inventory", MEEditorTheme.StylePrimaryButton))
                    {
                        if (manager.TryAddItem(addItemId, addAmount))
                        {
                            Debug.Log($"[InventoryManagerEditor] Successfully added {addAmount} of Item ID {addItemId}.");
                        }
                        else
                        {
                            Debug.LogWarning($"[InventoryManagerEditor] Failed to add Item ID {addItemId}. Inventory may be full.");
                        }
                    }

                    GUILayout.Space(8);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(8);
                }

                GUILayout.Label("<b>Inventory Commands & Rpc Syncs</b>", EditorStyles.miniBoldLabel);
                
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Sort Inventory", MEEditorTheme.StyleSecondaryButton))
                    {
                        manager.SortInventoryRpc();
                        Debug.Log("[InventoryManagerEditor] Triggered SortInventoryRpc.");
                    }

                    if (GUILayout.Button("Force Save", MEEditorTheme.StyleSecondaryButton))
                    {
                        manager.RequestSaveInventoryRpc();
                        Debug.Log("[InventoryManagerEditor] Triggered RequestSaveInventoryRpc.");
                    }

                    if (GUILayout.Button("Force Load", MEEditorTheme.StyleSecondaryButton))
                    {
                        manager.RequestLoadInventoryRpc();
                        Debug.Log("[InventoryManagerEditor] Triggered RequestLoadInventoryRpc.");
                    }
                }
                GUILayout.EndHorizontal();

                if (manager.IsServer)
                {
                    GUILayout.Space(4);
                    Color oldCol = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f);
                    if (GUILayout.Button("Clear Entire Inventory (Wipe Server)", MEEditorTheme.StylePrimaryButton))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Inventory Wipe", "Are you sure you want to clear this player's entire inventory?", "Wipe Slots", "Cancel"))
                        {
                            for (int i = 0; i < manager.Slots.Count; i++)
                            {
                                manager.Slots[i] = InventoryManager.InventoryItem.Empty;
                            }
                            Debug.Log("[InventoryManagerEditor] Inventory completely wiped on server.");
                        }
                    }
                    GUI.backgroundColor = oldCol;
                }
            }
            EndCard();

            // ── Card 4: Inventory Slots Content ──
            showInventorySlots = EditorGUILayout.Foldout(showInventorySlots, "<b>Inventory Slots Contents</b>", true, new GUIStyle(EditorStyles.foldout) { richText = true });
            if (showInventorySlots && manager.Slots != null)
            {
                BeginCard();
                {
                    if (manager.Slots.Count == 0)
                    {
                        GUILayout.Label("<i>Inventory Slots are Empty or Uninitialized.</i>", EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < manager.Slots.Count; i++)
                        {
                            var item = manager.Slots[i];
                            
                            GUILayout.BeginHorizontal(GUI.skin.box);
                            {
                                // Draw slot label
                                string slotPrefix = i < manager.QuickSlotCount ? $"<color=#66CD00>[Quick {i}]</color>" : $"Slot {i}";
                                GUILayout.Label(slotPrefix, new GUIStyle(EditorStyles.miniBoldLabel) { richText = true, alignment = TextAnchor.MiddleLeft }, GUILayout.Width(80));

                                if (item.IsEmpty)
                                {
                                    GUILayout.Label("<i>Empty</i>", EditorStyles.miniLabel);
                                    GUILayout.FlexibleSpace();
                                }
                                else
                                {
                                    var itemData = manager.GetItemData(item.itemId);
                                    string itemName = itemData != null ? itemData.itemName : $"Unknown Item (ID: {item.itemId})";
                                    
                                    // Visual Icon if available
                                    if (itemData != null && itemData.itemIcon != null)
                                    {
                                        Rect rect = GUILayoutUtility.GetRect(18, 18);
                                        DrawSprite(rect, itemData.itemIcon);
                                        GUILayout.Space(4);
                                    }

                                    GUILayout.Label($"<b>{itemName}</b> (x{item.count})", new GUIStyle(EditorStyles.miniLabel) { richText = true, alignment = TextAnchor.MiddleLeft });
                                    
                                    if (item.loadedAmmo >= 0)
                                    {
                                        GUILayout.Label($" <color=#FFB90F>[Ammo: {item.loadedAmmo}]</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                                    }

                                    if (itemData != null && itemData.HasDurability && itemData.maxDurability > 0)
                                    {
                                        float durPercent = item.GetDurabilityPercentage(itemData.maxDurability);
                                        Color durCol = itemData.GetDurabilityColor(durPercent);
                                        string durText = $"Durability: {durPercent * 100f:F0}%";
                                        GUILayout.Label($" <color=#{ColorUtility.ToHtmlStringRGB(durCol)}>[{durText}]</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                                    }

                                    GUILayout.FlexibleSpace();

                                    // Action buttons
                                    if (manager.IsServer)
                                    {
                                        if (GUILayout.Button("Drop", EditorStyles.miniButton, GUILayout.Width(45)))
                                        {
                                            manager.DropItemRpc(i, 1);
                                            Debug.Log($"[InventoryManagerEditor] Requesting drop of 1x {itemName} from Slot {i}.");
                                        }
                                        if (GUILayout.Button("Wipe", EditorStyles.miniButton, GUILayout.Width(45)))
                                        {
                                            manager.ConsumeItemRpc(i, item.count);
                                            Debug.Log($"[InventoryManagerEditor] Requesting wipe of Slot {i}.");
                                        }
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                EndCard();
            }

            Repaint();
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
