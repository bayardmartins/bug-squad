using System;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Multiplayer crafting table that allows players to craft weapons and tools.
    /// Server-authoritative with network-synced state.
    /// Only one player can interact with the UI at a time.
    /// </summary>
    public class CraftingTable : Interactable
    {
        #region Constants

        private const int MAX_SLOTS = 4;
        private const ulong NO_PLAYER = ulong.MaxValue;
        private const int NO_RECIPE = -1;

        #endregion

        #region Serialized Fields

        [Header("Crafting Table Settings")]
        [Tooltip("Database of all available recipes")]
        [SerializeField] private CraftingRecipeDatabase recipeDatabase;

        [Tooltip("Transform where crafted items will spawn")]
        [SerializeField] private Transform itemSpawnPoint;

        [Tooltip("Reference to world-space progress UI")]
        [SerializeField] private CraftingProgressWorldUI worldProgressUI;

        #endregion

        #region Network State

        /// <summary>
        /// Items currently stored in the crafting table slots.
        /// </summary>
        private NetworkList<CraftingSlotItem> storedItems;

        /// <summary>
        /// Currently selected recipe ID (-1 if none).
        /// </summary>
        private NetworkVariable<int> selectedRecipeId = new NetworkVariable<int>(
            NO_RECIPE, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// Crafting progress (0-1).
        /// </summary>
        private NetworkVariable<float> craftingProgress = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// Whether crafting is in progress.
        /// </summary>
        private NetworkVariable<bool> isCrafting = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// Client ID of the player currently interacting with the UI.
        /// </summary>
        private NetworkVariable<ulong> interactingPlayerId = new NetworkVariable<ulong>(
            NO_PLAYER, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Local State

        private CraftingTableUI localUI;
        private float craftingTimer;
        private CraftedItemTracker craftedItemTracker;

        #endregion

        #region Properties

        public CraftingRecipeDatabase RecipeDatabase => recipeDatabase;
        public int SelectedRecipeId => selectedRecipeId.Value;
        public float CraftingProgress => craftingProgress.Value;
        public bool IsCrafting => isCrafting.Value;
        public bool IsAvailable => interactingPlayerId.Value == NO_PLAYER;

        #endregion

        #region Events

        public static event Action<CraftingTable> OnTableStateChanged;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            storedItems = new NetworkList<CraftingSlotItem>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Initialize empty slots on server
            if (IsServer && storedItems.Count == 0)
            {
                for (int i = 0; i < MAX_SLOTS; i++)
                    storedItems.Add(CraftingSlotItem.Empty);
            }

            // Subscribe to state changes
            storedItems.OnListChanged += OnStoredItemsChanged;
            selectedRecipeId.OnValueChanged += OnRecipeChanged;
            craftingProgress.OnValueChanged += OnProgressChanged;
            isCrafting.OnValueChanged += OnCraftingStateChanged;
            interactingPlayerId.OnValueChanged += OnInteractingPlayerChanged;

            // Set interaction type
            interactionType = InteractionType.Interact;
        }

        public override void OnNetworkDespawn()
        {
            storedItems.OnListChanged -= OnStoredItemsChanged;
            selectedRecipeId.OnValueChanged -= OnRecipeChanged;
            craftingProgress.OnValueChanged -= OnProgressChanged;
            isCrafting.OnValueChanged -= OnCraftingStateChanged;
            interactingPlayerId.OnValueChanged -= OnInteractingPlayerChanged;
            
            // Cleanup spawned crafted item subscription
            if (craftedItemTracker != null)
            {
                craftedItemTracker.OnItemPickedUp -= OnCraftedItemDespawned;
                craftedItemTracker = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !isCrafting.Value) return;

            // Update crafting progress
            var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
            if (recipe != null && recipe.craftingTime > 0)
            {
                craftingTimer += Time.deltaTime;
                craftingProgress.Value = Mathf.Clamp01(craftingTimer / recipe.craftingTime);

                if (craftingTimer >= recipe.craftingTime)
                {
                    CompleteCrafting();
                }
            }
        }

        #endregion

        #region Interactable Override

        public override string GetDescription()
        {
            if (isCrafting.Value)
            {
                var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
                return recipe != null ? $"Crafting {recipe.recipeName}..." : "Crafting...";
            }
            return IsAvailable ? "Open crafting menu" : "In use";
        }

        public override void Interact()
        {
            // This is called on SERVER from InteractionController.ServerInteractRpc
            // The actual opening happens via TryInteract which handles clientId
            if (!IsServer) return;
        }

        /// <summary>
        /// Called by InteractionController when player wants to interact.
        /// Server checks availability and opens UI for the client.
        /// </summary>
        public void TryInteract(ulong clientId)
        {
            if (!IsServer) return;

            // Check if table is available
            if (!IsAvailable) return;

            // Lock the table to this player
            interactingPlayerId.Value = clientId;

            // Tell the client to open their UI
            OpenUIClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the item in a specific slot.
        /// </summary>
        public CraftingSlotItem GetSlotItem(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < storedItems.Count)
                return storedItems[slotIndex];
            return CraftingSlotItem.Empty;
        }

        /// <summary>
        /// Get all stored items.
        /// </summary>
        public CraftingSlotItem[] GetAllStoredItems()
        {
            var items = new CraftingSlotItem[MAX_SLOTS];
            for (int i = 0; i < MAX_SLOTS && i < storedItems.Count; i++)
                items[i] = storedItems[i];
            return items;
        }

        /// <summary>
        /// Check if local player is currently interacting.
        /// </summary>
        public bool IsLocalPlayerInteracting()
        {
            return interactingPlayerId.Value == NetworkManager.Singleton.LocalClientId;
        }

        /// <summary>
        /// Set reference to the local UI panel.
        /// </summary>
        public void SetLocalUI(CraftingTableUI ui)
        {
            localUI = ui;
        }

        #endregion

        #region Server RPCs

        /// <summary>
        /// Request to open the crafting UI.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestOpenUIRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Check if table is available
            if (!IsAvailable) return;

            // Lock the table to this player
            interactingPlayerId.Value = clientId;

            // Tell the client to open their UI
            OpenUIClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Close the UI and release the interaction lock.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void CloseUIRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only the interacting player can close
            if (interactingPlayerId.Value != clientId)
                return;

            // Release the lock
            interactingPlayerId.Value = NO_PLAYER;
        }

        /// <summary>
        /// Select a recipe to craft.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SelectRecipeRpc(int recipeId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only interacting player can change recipe
            if (interactingPlayerId.Value != clientId)
                return;

            // Can't change recipe while crafting
            if (isCrafting.Value)
                return;

            selectedRecipeId.Value = recipeId;
        }

        /// <summary>
        /// Add an ingredient from player's inventory to the table.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void AddIngredientRpc(int itemId, int amount, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only interacting player can add items
            if (interactingPlayerId.Value != clientId)
                return;

            // Can't add while crafting
            if (isCrafting.Value)
                return;

            // Find the player's inventory
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;

            var inventory = playerObj.GetComponent<InventoryManager>();
            if (inventory == null) return;

            // Check if player has the item
            if (inventory.GetTotalCount(itemId) < amount)
                return;

            // Find a slot with same item or empty slot
            int targetSlot = -1;
            for (int i = 0; i < storedItems.Count; i++)
            {
                if (storedItems[i].itemId == itemId)
                {
                    targetSlot = i;
                    break;
                }
            }

            if (targetSlot == -1)
            {
                for (int i = 0; i < storedItems.Count; i++)
                {
                    if (storedItems[i].IsEmpty)
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot == -1)
                return; // No available slot

            // Remove from inventory (find and consume from slots)
            int remaining = amount;
            for (int i = 0; i < inventory.Slots.Count && remaining > 0; i++)
            {
                var slot = inventory.Slots[i];
                if (slot.itemId == itemId && !slot.IsEmpty)
                {
                    int toRemove = Mathf.Min(remaining, slot.count);
                    inventory.ConsumeItemRpc(i, toRemove);
                    remaining -= toRemove;
                }
            }

            // Add to crafting table
            var existingItem = storedItems[targetSlot];
            if (existingItem.IsEmpty)
            {
                storedItems[targetSlot] = new CraftingSlotItem { itemId = itemId, amount = amount };
            }
            else
            {
                existingItem.amount += amount;
                storedItems[targetSlot] = existingItem;
            }
        }

        /// <summary>
        /// Remove an ingredient from the table back to player's inventory.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RemoveIngredientRpc(int slotIndex, int amount, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only interacting player can remove items
            if (interactingPlayerId.Value != clientId)
                return;

            // Can't remove while crafting
            if (isCrafting.Value)
                return;

            if (slotIndex < 0 || slotIndex >= storedItems.Count)
                return;

            var slotItem = storedItems[slotIndex];
            if (slotItem.IsEmpty)
                return;

            int actualAmount = Mathf.Min(amount, slotItem.amount);

            // Find the player's inventory
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;

            var inventory = playerObj.GetComponent<InventoryManager>();
            if (inventory == null) return;

            // Add back to inventory
            if (inventory.TryAddItem(slotItem.itemId, actualAmount))
            {
                // Remove from table
                slotItem.amount -= actualAmount;
                if (slotItem.amount <= 0)
                    storedItems[slotIndex] = CraftingSlotItem.Empty;
                else
                    storedItems[slotIndex] = slotItem;
            }
        }

        /// <summary>
        /// Start crafting the selected recipe.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void StartCraftingRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only interacting player can start crafting
            if (interactingPlayerId.Value != clientId)
                return;

            // Already crafting
            if (isCrafting.Value)
                return;

            // Check recipe is selected
            var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
            if (recipe == null)
                return;

            // Check if all ingredients are present
            if (!recipe.CanCraft(GetAllStoredItems()))
                return;

            // Consume ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                int toConsume = ingredient.amount;
                for (int i = 0; i < storedItems.Count && toConsume > 0; i++)
                {
                    if (storedItems[i].itemId == ingredient.item.itemId)
                    {
                        var slot = storedItems[i];
                        int consumed = Mathf.Min(toConsume, slot.amount);
                        slot.amount -= consumed;
                        toConsume -= consumed;

                        if (slot.amount <= 0)
                            storedItems[i] = CraftingSlotItem.Empty;
                        else
                            storedItems[i] = slot;
                    }
                }
            }

            // Release interaction lock so player can leave
            interactingPlayerId.Value = NO_PLAYER;

            // Start crafting
            craftingTimer = 0f;
            craftingProgress.Value = 0f;
            isCrafting.Value = true;

            // Close the UI for the player
            CloseUIClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Start crafting with direct inventory consumption.
        /// Consumes from table first, then from player inventory for any remaining amounts.
        /// Supports both manual ingredient contribution and direct crafting.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void StartCraftingDirectRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Only interacting player can start crafting
            if (interactingPlayerId.Value != clientId)
                return;

            // Already crafting
            if (isCrafting.Value)
                return;

            // Check recipe is selected
            var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
            if (recipe == null)
                return;

            // Find the player's inventory
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;

            var inventory = playerObj.GetComponent<InventoryManager>();
            if (inventory == null) return;

            // Check if combined table + inventory has enough ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                int storedAmount = 0;
                foreach (var item in storedItems)
                {
                    if (item.itemId == ingredient.item.itemId)
                        storedAmount += item.amount;
                }
                int inventoryAmount = inventory.GetTotalCount(ingredient.item.itemId);

                if (storedAmount + inventoryAmount < ingredient.amount)
                    return;
            }

            // Consume ingredients - first from table, then from inventory
            foreach (var ingredient in recipe.ingredients)
            {
                int toConsume = ingredient.amount;

                // First consume from table storage
                for (int i = 0; i < storedItems.Count && toConsume > 0; i++)
                {
                    if (storedItems[i].itemId == ingredient.item.itemId)
                    {
                        var slot = storedItems[i];
                        int consumed = Mathf.Min(toConsume, slot.amount);
                        slot.amount -= consumed;
                        toConsume -= consumed;

                        if (slot.amount <= 0)
                            storedItems[i] = CraftingSlotItem.Empty;
                        else
                            storedItems[i] = slot;
                    }
                }

                // Then consume remaining from player inventory
                if (toConsume > 0)
                {
                    for (int i = 0; i < inventory.Slots.Count && toConsume > 0; i++)
                    {
                        var invSlot = inventory.Slots[i];
                        if (invSlot.itemId == ingredient.item.itemId && !invSlot.IsEmpty)
                        {
                            int toRemove = Mathf.Min(toConsume, invSlot.count);
                            inventory.ConsumeItemRpc(i, toRemove);
                            toConsume -= toRemove;
                        }
                    }
                }
            }

            // Release interaction lock so player can leave
            interactingPlayerId.Value = NO_PLAYER;

            // Start crafting
            craftingTimer = 0f;
            craftingProgress.Value = 0f;
            isCrafting.Value = true;

            // Close the UI for the player
            CloseUIClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Cancel crafting (returns partial ingredients? or just cancels?).
        /// For simplicity, canceling does not return ingredients.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void CancelCraftingRpc(RpcParams rpcParams = default)
        {
            // Note: Only server/admin should be able to cancel? 
            // For now, anyone can cancel if needed
            if (!isCrafting.Value)
                return;

            isCrafting.Value = false;
            craftingProgress.Value = 0f;
            craftingTimer = 0f;
            // Recipe remains selected, ingredients are consumed
        }

        #endregion

        #region Client RPCs

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenUIClientRpc(RpcParams rpcParams = default)
        {
            // Find or create the UI
            if (localUI == null)
                localUI = CraftingTableUI.Instance;

            if (localUI != null)
                localUI.Open(this);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void CloseUIClientRpc(RpcParams rpcParams = default)
        {
            if (localUI != null)
            {
                localUI.Close();
            }
        }

        #endregion

        #region Private Methods

        private void CompleteCrafting()
        {
            var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
            if (recipe == null || recipe.resultItem == null)
            {
                ResetCraftingState();
                return;
            }

            // Spawn the crafted item at the spawn point
            Vector3 spawnPos = itemSpawnPoint != null ? itemSpawnPoint.position : transform.position + Vector3.up * 0.5f;
            Quaternion spawnRot = itemSpawnPoint != null ? itemSpawnPoint.rotation : Quaternion.identity;

            if (recipe.resultItem.networkPrefab != null)
            {
                var spawnedObj = Instantiate(recipe.resultItem.networkPrefab, spawnPos, spawnRot);
                var netObj = spawnedObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    
                    // Track the spawned item to detect when it's picked up
                    craftedItemTracker = spawnedObj.AddComponent<CraftedItemTracker>();
                    craftedItemTracker.OnItemPickedUp += OnCraftedItemDespawned;
                }
                else
                {
                    Destroy(spawnedObj);
                }
            }

            ResetCraftingState();
        }

        private void ResetCraftingState()
        {
            isCrafting.Value = false;
            craftingProgress.Value = 0f;
            craftingTimer = 0f;
            selectedRecipeId.Value = NO_RECIPE;
        }
        
        /// <summary>
        /// Called when the crafted item is picked up (despawned) by any player.
        /// Hides the world progress UI.
        /// </summary>
        private void OnCraftedItemDespawned()
        {
            if (craftedItemTracker != null)
            {
                craftedItemTracker.OnItemPickedUp -= OnCraftedItemDespawned;
                craftedItemTracker = null;
            }
            
            // Hide the world UI now that item is picked up
            if (worldProgressUI != null)
                worldProgressUI.Hide();
        }

        #endregion

        #region Network Callbacks

        private void OnStoredItemsChanged(NetworkListEvent<CraftingSlotItem> changeEvent)
        {
            OnTableStateChanged?.Invoke(this);
        }

        private void OnRecipeChanged(int previous, int current)
        {
            OnTableStateChanged?.Invoke(this);
        }

        private void OnProgressChanged(float previous, float current)
        {
            // Update world UI
            if (worldProgressUI != null)
            {
                worldProgressUI.UpdateProgress(current, isCrafting.Value);
            }
            OnTableStateChanged?.Invoke(this);
        }

        private void OnCraftingStateChanged(bool previous, bool current)
        {
            if (worldProgressUI != null)
            {
                var recipe = recipeDatabase?.GetRecipeById(selectedRecipeId.Value);
                
                // When crafting starts, configure the world UI
                if (current && recipe != null)
                {
                    worldProgressUI.SetEnabled(true);
                    worldProgressUI.SetCraftingTime(recipe.craftingTime);
                    worldProgressUI.SetItemIcon(recipe.resultItem?.itemIcon);
                }
                
                worldProgressUI.SetCrafting(current, recipe?.recipeName ?? "");
            }
            OnTableStateChanged?.Invoke(this);
        }

        private void OnInteractingPlayerChanged(ulong previous, ulong current)
        {
            OnTableStateChanged?.Invoke(this);
        }

        #endregion
    }
}