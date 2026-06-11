using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Server-authoritative inventory manager with NetworkList synchronization.
    /// Simplified design: Quick slots = first 8 inventory slots.
    /// </summary>
    public class InventoryManager : NetworkBehaviour, IInventoryManager
    {
        #region Data Structures

        [Serializable]
        public struct InventoryItem : INetworkSerializable, IEquatable<InventoryItem>
        {
            public int itemId;
            public int count;
            public int durability;
            /// <summary>
            /// Rounds currently loaded in a shooter weapon's magazine.
            /// -1 means "not a shooter weapon" (default for non-gun items).
            /// 0 means empty magazine (gun needs reload).
            /// </summary>
            public int loadedAmmo;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                short shortItemId = (short)itemId;
                serializer.SerializeValue(ref shortItemId);
                if (serializer.IsReader)
                    itemId = shortItemId;

                if (itemId != -1)
                {
                    short shortCount = (short)count;
                    serializer.SerializeValue(ref shortCount);
                    if (serializer.IsReader)
                        count = shortCount;

                    short shortDurability = (short)durability;
                    serializer.SerializeValue(ref shortDurability);
                    if (serializer.IsReader)
                        durability = shortDurability;

                    short shortLoadedAmmo = (short)loadedAmmo;
                    serializer.SerializeValue(ref shortLoadedAmmo);
                    if (serializer.IsReader)
                        loadedAmmo = shortLoadedAmmo;
                }
                else
                {
                    if (serializer.IsReader)
                    {
                        count = 0;
                        durability = 0;
                        loadedAmmo = -1;
                    }
                }
            }

            public static InventoryItem Empty => new InventoryItem { itemId = -1, count = 0, durability = 0, loadedAmmo = -1 };
            public bool IsEmpty => itemId == -1 || count <= 0;

            public bool Equals(InventoryItem other) => 
                itemId == other.itemId && count == other.count && durability == other.durability && loadedAmmo == other.loadedAmmo;

            public static InventoryItem Create(int id, int amount, int maxDurability = -1, int loadedAmmo = -1) =>
                new InventoryItem { itemId = id, count = amount, durability = maxDurability > 0 ? maxDurability : -1, loadedAmmo = loadedAmmo };

            public float GetDurabilityPercentage(int maxDurability) =>
                maxDurability <= 0 || durability < 0 ? 1f : Mathf.Clamp01((float)durability / maxDurability);
        }

        [Serializable]
        private class InventorySaveData
        {
            public string ownerId;
            public List<InventoryItem> items = new List<InventoryItem>();
            public long timestamp;
        }

        #endregion

        #region Fields & Properties

        [Header("Configuration")]
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private int maxInventorySize = 20;
        [SerializeField] private int quickSlotCount = 8;

        private NetworkList<InventoryItem> slots;

        // Public properties
        public NetworkList<InventoryItem> Slots => slots;
        public int MaxInventorySize => maxInventorySize;
        public int QuickSlotCount => Mathf.Min(quickSlotCount, maxInventorySize);
        public IReadOnlyList<InventoryItemData> DBItems => itemDatabase != null ? itemDatabase.Items : null;

        public event Action<NetworkList<InventoryItem>> OnInventoryChanged;

        private NetworkVariable<Unity.Collections.FixedString64Bytes> netPlayerId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // IMPORTANT: NetworkList must be initialized in Awake, before OnNetworkSpawn
            slots = new NetworkList<InventoryItem>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Server populates the inventory slots initially empty
            if (IsServer && slots.Count == 0)
            {
                // Ensure SaveGameManager exists (auto-creates for testing without lobby)
                SaveGameManager.EnsureInstance();
                SaveGameManager.Instance.ActivateTestIdIfNeeded();

                for (int i = 0; i < maxInventorySize; i++)
                    slots.Add(InventoryItem.Empty);
            }

            if (IsOwner)
            {
                InventoryUI.Instance?.Initialize(this);
                slots.OnListChanged += OnSlotsChanged;
                
                // If there's a profile, inform the server of our player ID so it can load the right save file
                if (PlayerProfileManager.Instance?.LocalPlayerStats != null)
                {
                    SetPlayerIdServerRpc(PlayerProfileManager.Instance.LocalPlayerStats.PlayerId);
                }
            }
            
            if (IsServer)
            {
                if (!string.IsNullOrEmpty(netPlayerId.Value.ToString()))
                {
                    Load();
                }
                SaveGameManager.OnAutoSave += Save;
            }
        }

        [Rpc(SendTo.Server)]
        private void SetPlayerIdServerRpc(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            
            netPlayerId.Value = playerId;
            Load();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SaveGameManager.OnAutoSave -= Save;
                Save();
            }

            if (IsOwner && slots != null)
                slots.OnListChanged -= OnSlotsChanged;

            base.OnNetworkDespawn();
        }

        // Save debouncing to prevent N saves during bulk operations (e.g. sort)
        private bool saveDirty;
        private float saveDirtyTime;
        private const float SAVE_DEBOUNCE = 1.5f;

        private void Update()
        {
            if (IsServer && saveDirty && Time.time - saveDirtyTime > SAVE_DEBOUNCE)
            {
                saveDirty = false;
                Save();
            }
        }

        private void OnSlotsChanged(NetworkListEvent<InventoryItem> e)
        {
            OnInventoryChanged?.Invoke(slots);
            if (IsServer)
            {
                saveDirty = true;
                saveDirtyTime = Time.time;
            }
        }

        #endregion

        #region Item Database

        public InventoryItemData GetItemData(int itemId)
        {
            if (itemDatabase == null) return null;
            return itemDatabase.GetItemByID(itemId);
        }

        public bool HasItem(int itemId) => GetTotalCount(itemId) > 0;

        public int GetTotalCount(int itemId)
        {
            int total = 0;
            foreach (var item in slots)
            {
                if (item.itemId == itemId && !item.IsEmpty)
                    total += item.count;
            }
            return total;
        }

        // Backwards compatibility alias
        public int GetAvailableCount(int itemId) => GetTotalCount(itemId);

        #endregion

        #region Slot Operations

        public InventoryItem GetItemAt(int index) => 
            IsValidIndex(index) ? slots[index] : InventoryItem.Empty;

        public void RemoveItemAt(int index)
        {
            if (IsServer && IsValidIndex(index))
                slots[index] = InventoryItem.Empty;
        }

        private bool IsValidIndex(int index) => index >= 0 && index < slots.Count;

        private int FindEmptySlot()
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].IsEmpty) return i;
            return -1;
        }

        /// <summary>
        /// Public wrapper to find the first empty inventory slot.
        /// </summary>
        public int FindFirstEmptySlot() => FindEmptySlot();

        private int CountEmptySlots()
        {
            int count = 0;
            foreach (var item in slots)
                if (item.IsEmpty) count++;
            return count;
        }

        #endregion

        #region Add Items

        public bool TryAddToInventory(Pickable pickable)
        {
            if (!IsServer) return false;

            int itemId = pickable.InventoryItemData.itemId;
            int amount = pickable.Amount;
            int maxStack = pickable.InventoryItemData.maxStack;
            int maxDurability = pickable.InventoryItemData.maxDurability;
            var objectType = pickable.ObjectType;

            // Get actual durability from pickable (for tools/weapons)
            int actualDurability = pickable.GetActualDurability();

            // Get loaded ammo from pickable (for shooter weapons)
            int pickableLoadedAmmo = pickable.GetLoadedAmmo();

            // Non-stackable items (weapons, tools) - each gets own slot
            if (objectType == ObjectType.Weapon || objectType == ObjectType.Tools)
            {
                if (CountEmptySlots() < amount) return false;

                for (int i = 0; i < amount; i++)
                {
                    int slot = FindEmptySlot();
                    if (slot != -1)
                        slots[slot] = InventoryItem.Create(itemId, 1, actualDurability > 0 ? actualDurability : maxDurability, pickableLoadedAmmo);
                }
                return true;
            }

            // Stackable items
            int remaining = amount;

            // Fill existing stacks first
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].itemId == itemId && !slots[i].IsEmpty)
                {
                    int space = maxStack - slots[i].count;
                    int toAdd = Mathf.Min(remaining, space);
                    if (toAdd > 0)
                    {
                        var item = slots[i];
                        item.count += toAdd;
                        slots[i] = item;
                        remaining -= toAdd;
                    }
                }
            }

            // Create new stacks
            while (remaining > 0)
            {
                int slot = FindEmptySlot();
                if (slot == -1) return false;

                int stackAmount = Mathf.Min(remaining, maxStack);
                slots[slot] = InventoryItem.Create(itemId, stackAmount, maxDurability);
                remaining -= stackAmount;
            }

            return true;
        }

        /// <summary>
        /// Try to add item by ID to inventory (for drops, rewards, etc.)
        /// </summary>
        public bool TryAddItem(int itemId, int amount = 1)
        {
            if (!IsServer) return false;

            var itemData = GetItemData(itemId);
            if (itemData == null) return false;

            int maxStack = itemData.maxStack;
            int maxDurability = itemData.maxDurability;
            var objectType = itemData.objectType;

            // Non-stackable items
            if (objectType == ObjectType.Weapon || objectType == ObjectType.Tools)
            {
                if (CountEmptySlots() < amount) return false;

                for (int i = 0; i < amount; i++)
                {
                    int slot = FindEmptySlot();
                    if (slot == -1) return false;
                    slots[slot] = InventoryItem.Create(itemId, 1, maxDurability);
                }
                return true;
            }

            // Stackable items
            int remaining = amount;

            // Fill existing stacks first
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].itemId == itemId && !slots[i].IsEmpty)
                {
                    int space = maxStack - slots[i].count;
                    int toAdd = Mathf.Min(remaining, space);
                    if (toAdd > 0)
                    {
                        var item = slots[i];
                        item.count += toAdd;
                        slots[i] = item;
                        remaining -= toAdd;
                    }
                }
            }

            // Create new stacks
            while (remaining > 0)
            {
                int slot = FindEmptySlot();
                if (slot == -1) return false;

                int stackAmount = Mathf.Min(remaining, maxStack);
                slots[slot] = InventoryItem.Create(itemId, stackAmount, maxDurability);
                remaining -= stackAmount;
            }

            return true;
        }

        #endregion

        #region RPCs - Swap & Move

        [Rpc(SendTo.Server)]
        public void SwapSlotsRpc(int fromIndex, int toIndex)
        {
            if (!IsServer || !IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
                return;

            var fromItem = slots[fromIndex];
            var toItem = slots[toIndex];

            if (fromItem.IsEmpty) return;

            // Empty destination - just move
            if (toItem.IsEmpty)
            {
                slots[toIndex] = fromItem;
                slots[fromIndex] = InventoryItem.Empty;
                CheckEquippedSlotChanged(fromIndex, toIndex);
                return;
            }

            // Same item type - try merge
            if (fromItem.itemId == toItem.itemId)
            {
                var itemData = GetItemData(fromItem.itemId);
                if (itemData != null && itemData.maxStack > 1)
                {
                    int totalCount = fromItem.count + toItem.count;
                    if (totalCount <= itemData.maxStack)
                    {
                        toItem.count = totalCount;
                        slots[toIndex] = toItem;
                        slots[fromIndex] = InventoryItem.Empty;
                    }
                    else
                    {
                        toItem.count = itemData.maxStack;
                        fromItem.count = totalCount - itemData.maxStack;
                        slots[toIndex] = toItem;
                        slots[fromIndex] = fromItem;
                    }
                    CheckEquippedSlotChanged(fromIndex, toIndex);
                    return;
                }
            }

            // Different items - swap positions
            slots[fromIndex] = toItem;
            slots[toIndex] = fromItem;
            CheckEquippedSlotChanged(fromIndex, toIndex);
        }

        // Backwards compatibility alias
        [Rpc(SendTo.Server)]
        public void SwapRpc(int fromIndex, int toIndex) => SwapSlotsRpc(fromIndex, toIndex);

        [Rpc(SendTo.Server)]
        public void SplitStackRpc(int fromIndex, int toIndex, int amount)
        {
            if (!IsServer || !IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
                return;

            var fromItem = slots[fromIndex];
            var toItem = slots[toIndex];

            if (fromItem.IsEmpty || amount <= 0 || amount >= fromItem.count)
                return;

            if (!toItem.IsEmpty && fromItem.itemId != toItem.itemId)
                return;

            var itemData = GetItemData(fromItem.itemId);
            if (itemData == null) return;

            if (toItem.IsEmpty)
            {
                // Create new stack
                slots[toIndex] = new InventoryItem
                {
                    itemId = fromItem.itemId,
                    count = amount,
                    durability = fromItem.durability
                };
                fromItem.count -= amount;
                slots[fromIndex] = fromItem;
            }
            else
            {
                // Add to existing stack
                int space = itemData.maxStack - toItem.count;
                int actualAmount = Mathf.Min(amount, space);
                if (actualAmount > 0)
                {
                    toItem.count += actualAmount;
                    fromItem.count -= actualAmount;
                    slots[toIndex] = toItem;
                    slots[fromIndex] = fromItem;
                }
            }
        }

        #endregion

        #region RPCs - Drop Items

        [Rpc(SendTo.Server)]
        public void DropItemRpc(int slotIndex, int amount = -1)
        {
            if (!IsServer || !IsValidIndex(slotIndex))
                return;

            var item = slots[slotIndex];
            if (item.IsEmpty) return;

            var itemData = GetItemData(item.itemId);
            if (itemData == null || itemData.networkPrefab == null)
            {
                Debug.LogWarning("DropItemRpc: Invalid item data or missing prefab");
                return;
            }

            // Determine amount to drop
            int dropAmount = amount > 0 ? Mathf.Min(amount, item.count) : item.count;

            // Calculate drop position (in front of player)
            Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

            // Spawn the item
            var droppedObj = Instantiate(itemData.networkPrefab, dropPos, Quaternion.identity);
            var netObj = droppedObj.GetComponent<NetworkObject>();
            
            if (netObj != null)
            {
                netObj.Spawn();

                // Set the pickable's amount and durability
                var pickable = droppedObj.GetComponent<Pickable>();
                if (pickable != null)
                {
                    pickable.UpdateAmount(dropAmount);
                    
                    // For tools/weapons, set durability from inventory
                    if (itemData.objectType == ObjectType.Weapon || itemData.objectType == ObjectType.Tools)
                    {
                        int durability = item.durability > 0 ? item.durability : itemData.maxDurability;
                        pickable.SetDurability(durability);
                    }

                    // For shooter weapons, set loaded ammo from inventory
                    if (item.loadedAmmo >= 0)
                    {
                        pickable.SetLoadedAmmo(item.loadedAmmo);
                    }
                }

                // Apply drop physics
                var rb = droppedObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 force = transform.forward * 3f + Vector3.up * 2f;
                    force += new Vector3(
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        UnityEngine.Random.Range(0f, 0.5f),
                        UnityEngine.Random.Range(-0.5f, 0.5f)
                    );
                    rb.linearVelocity = force;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
                }

                // Update inventory
                bool slotFullyEmptied = dropAmount >= item.count;
                if (slotFullyEmptied)
                {
                    slots[slotIndex] = InventoryItem.Empty;
                }
                else
                {
                    item.count -= dropAmount;
                    slots[slotIndex] = item;
                }

                // If the slot was fully emptied, check if it was the equipped slot
                if (slotFullyEmptied)
                {
                    CheckEquippedSlotChanged(slotIndex);
                }

                Debug.Log($"Dropped {dropAmount}x {itemData.itemName}");
            }
            else
            {
                Debug.LogError("DropItemRpc: Missing NetworkObject on prefab");
                Destroy(droppedObj);
            }
        }

        #endregion

        #region Equipment Slot Change Detection

        /// <summary>
        /// Checks if any of the given slot indices is the currently equipped slot,
        /// and if so, notifies the owning client to update its equipment.
        /// Uses both slot index and item ID matching for dedicated server compatibility.
        /// </summary>
        private void CheckEquippedSlotChanged(params int[] slotIndices)
        {
            var equipController = GetComponent<EquipmentController>();
            if (equipController == null || equipController.EquippedItemId < 0) return;

            int equippedSlot = equipController.CurrentSlotIndex;

            foreach (int idx in slotIndices)
            {
                // Check by slot index (works in host mode)
                if (equippedSlot >= 0 && idx == equippedSlot)
                {
                    NotifyEquippedSlotChangedRpc();
                    return;
                }

                // Fallback: check by item ID (for dedicated server where slot index isn't tracked)
                if (equippedSlot < 0)
                {
                    var slotItem = slots[idx];
                    if (!slotItem.IsEmpty && slotItem.itemId == equipController.EquippedItemId)
                    {
                        NotifyEquippedSlotChangedRpc();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Notifies the owning client that the equipped slot's contents have changed.
        /// The EquipmentController will re-evaluate and re-equip or unequip as needed.
        /// </summary>
        [Rpc(SendTo.Owner)]
        private void NotifyEquippedSlotChangedRpc()
        {
            var equipController = GetComponent<EquipmentController>();
            equipController?.HandleEquippedSlotChanged();
        }

        #endregion

        #region RPCs - Durability

        [Rpc(SendTo.Server)]
        public void ReduceDurabilityRpc(int slotIndex, int damageAmount = 1)
        {
            if (!IsServer || !IsValidIndex(slotIndex))
                return;

            var item = slots[slotIndex];
            if (item.IsEmpty || item.durability < 0) return;

            var itemData = GetItemData(item.itemId);
            if (itemData == null || itemData.maxDurability <= 0) return;

            item.durability = Mathf.Max(0, item.durability - damageAmount);

            if (item.durability <= 0)
            {
                slots[slotIndex] = InventoryItem.Empty;
                Debug.Log($"{itemData.itemName} broke!");
            }
            else
            {
                slots[slotIndex] = item;
            }
        }

        /// <summary>
        /// Consumes (removes) items from a slot without dropping.
        /// Used by consumable items.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ConsumeItemRpc(int slotIndex, int amount = 1)
        {
            if (!IsServer || !IsValidIndex(slotIndex))
                return;

            var item = slots[slotIndex];
            if (item.IsEmpty || amount <= 0) return;

            if (amount >= item.count)
            {
                // Remove entire stack
                slots[slotIndex] = InventoryItem.Empty;
            }
            else
            {
                // Reduce count
                item.count -= amount;
                slots[slotIndex] = item;
            }
        }

        [Rpc(SendTo.Server)]
        public void RemoveItemByIDRpc(int itemId, int amount)
        {
            if (!IsServer || amount <= 0) return;

            int remaining = amount;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].itemId == itemId && !slots[i].IsEmpty)
                {
                    int toConsume = Mathf.Min(remaining, slots[i].count);

                    if (toConsume >= slots[i].count)
                    {
                        slots[i] = InventoryItem.Empty;
                    }
                    else
                    {
                        var item = slots[i];
                        item.count -= toConsume;
                        slots[i] = item;
                    }
                    
                    remaining -= toConsume;
                }
            }
        }

        [Rpc(SendTo.Server)]
        public void RepairItemRpc(int slotIndex, int repairAmount)
        {
            if (!IsServer || !IsValidIndex(slotIndex))
                return;

            var item = slots[slotIndex];
            if (item.IsEmpty || item.durability < 0) return;

            var itemData = GetItemData(item.itemId);
            if (itemData == null || itemData.maxDurability <= 0) return;

            item.durability = Mathf.Min(itemData.maxDurability, item.durability + repairAmount);
            slots[slotIndex] = item;
        }

        /// <summary>
        /// Updates the loaded ammo count for a shooter weapon in the specified slot.
        /// Called by ShooterWeaponHandler to persist magazine state on unequip/switch.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SetLoadedAmmoRpc(int slotIndex, int ammo)
        {
            if (!IsServer || !IsValidIndex(slotIndex))
                return;

            var item = slots[slotIndex];
            if (item.IsEmpty) return;

            item.loadedAmmo = ammo;
            slots[slotIndex] = item;
        }

        #endregion

        #region RPCs - Utility

        [Rpc(SendTo.Server)]
        public void SortInventoryRpc()
        {
            if (!IsServer) return;

            // Collect non-empty items
            var items = new List<InventoryItem>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                    items.Add(slots[i]);
            }

            // Sort by item ID, then by count (descending)
            items.Sort((a, b) =>
            {
                int cmp = a.itemId.CompareTo(b.itemId);
                return cmp != 0 ? cmp : b.count.CompareTo(a.count);
            });

            // Clear and refill
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i] = i < items.Count ? items[i] : InventoryItem.Empty;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestSaveInventoryRpc() => Save();

        [Rpc(SendTo.Server)]
        public void RequestLoadInventoryRpc() => Load();

        #endregion

        #region Save/Load

        private async void Save()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId)) return; // No active game — skip save

            try
            {
                string ownerId = GetOwnerId();
                if (string.IsNullOrEmpty(ownerId)) return;

                var data = new InventorySaveData
                {
                    ownerId = ownerId,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                foreach (var item in slots)
                    data.items.Add(item);

                await SaveGameManager.Instance.SavePlayerSubsystemToHostAsync(gameId, ownerId, "inventory", data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save inventory: {e.Message}");
            }
        }

        private async void Load()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId)) return; // No active game — start fresh

            try
            {
                string ownerId = GetOwnerId();
                if (string.IsNullOrEmpty(ownerId)) return;

                var data = await SaveGameManager.Instance.LoadPlayerSubsystemFromHostAsync<InventorySaveData>(gameId, ownerId, "inventory");
                ApplyLoadedData(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load inventory: {e.Message}");
                ApplyLoadedData(null);
            }
        }

        private void ApplyLoadedData(InventorySaveData data)
        {
            if (!IsServer) return;

            slots.Clear();
            int count = data?.items != null ? Mathf.Min(data.items.Count, maxInventorySize) : 0;
            
            for (int i = 0; i < count; i++)
                slots.Add(data.items[i]);

            while (slots.Count < maxInventorySize)
                slots.Add(InventoryItem.Empty);
        }

        /// <summary>
        /// Resolves the owner key for save data.
        /// </summary>
        private string GetOwnerId()
        {
            string playerId = GetPlayerId();
            if (string.IsNullOrEmpty(playerId)) return null;

            return SaveGameManager.Instance?.GetOwnerKey(playerId) ?? playerId;
        }

        private string GetPlayerId()
        {
            if (!string.IsNullOrEmpty(netPlayerId.Value.ToString()))
            {
                return netPlayerId.Value.ToString();
            }

            if (PlayerProfileManager.Instance?.LocalPlayerStats != null &&
                NetworkManager.Singleton.LocalClientId == OwnerClientId)
            {
                return PlayerProfileManager.Instance.LocalPlayerStats.PlayerId;
            }
            return OwnerClientId.ToString();
        }

        #endregion
    }

    public enum ObjectType
    {
        Resource,
        Weapon,
        Consumable,
        Tools
    }
}