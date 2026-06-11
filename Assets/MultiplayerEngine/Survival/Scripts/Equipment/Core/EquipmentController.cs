using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Type of equipment swap being performed.
    /// </summary>
    public enum SwapType
    {
        None,           // No swap needed
        ItemToItem,     // Switching from one item to another
        ItemToEmpty,    // Unequipping item to empty slot
        EmptyToItem     // Equipping item from empty hands
    }

    /// <summary>
    /// Main controller for equipped items. Handles input routing, item switching,
    /// and network synchronization for equipment actions.
    /// </summary>
    public class EquipmentController : NetworkBehaviour
    {
        [Header("Hold Points")]
        [SerializeField] private Transform rightHandHoldPoint;
        [SerializeField] private Transform leftHandHoldPoint;

        [Header("Animation")]
        [SerializeField] private Animator playerAnimator;
        public Animator PlayerAnimator => playerAnimator;
        
        [Header("Upper Body Animation")]
        [Tooltip("Index of the Upper Body layer in the Animator Controller")]
        [SerializeField] private int upperBodyLayerIndex = 1;
        [Tooltip("Speed at which the layer weight blends")]
        [SerializeField] private float layerBlendSpeed = 8f;

        [Header("References")]
        [SerializeField] private IInputManager inputManager;

        [Header("Character IK")]
        [Tooltip("Optional per-character IK/hand offset overrides. If assigned, overrides per-item IK & hand offset data.")]
        [SerializeField] private CharacterIKData characterIKData;

        // Components
        private IInventoryManager inventoryManager;
        private IStaminaManager staminaManager;
        private PlayerController playerController;
        private ShooterIKController shooterIKController;

        // Current equipment state
        private IItemHandler currentHandler;
        private int currentSlotIndex = -1;
        private int currentItemId = -1;

        /// <summary>
        /// The inventory slot index of the currently equipped item, or -1 if nothing equipped.
        /// </summary>
        public int CurrentSlotIndex => currentSlotIndex;

        /// <summary>
        /// The item ID of the currently equipped item, or -1 if nothing equipped.
        /// Synced via NetworkVariable, readable on all clients and server.
        /// </summary>
        public int EquippedItemId => equippedItemId.Value;

        // Animation state
        private RuntimeAnimatorController baseAnimatorController;

        // Debounce system for weapon swap
        private float swapDebounceTimer = 0f;
        private const float SWAP_DEBOUNCE_DELAY = 0.15f;
        private int targetSlotIndex = -1;  // The slot player wants to switch to
        private bool hasPendingSwap = false;

        // Animation phase tracking for mid-swap input
        private bool isSwapAnimationPlaying = false;
        
        // Upper body layer animation
        private float targetLayerWeight = 0f;
        private bool isBlendingLayerWeight = false;
        private SwapType currentSwapType = SwapType.None;
        private int activeLayerIndex = -1;  // Current item's layer index, -1 means use default

        // Combo attack layer management - prevents UpdateLayerWeight from fighting with ComboAttackBehavior
        private bool isInComboAttack = false;
        private int comboLayerIndex = -1;

        // Action layer management - separate layer for tool/consumable actions
        private bool isInAction = false;
        private bool isActionBlendingOut = false;  // True while blending action layer back to 0
        private int actionLayerIdx = -1;  // The active action layer, -1 means no separate action layer
        private bool pendingBaseAnimatorRestore = false; // Delays base animator restore to prevent animation popping

        // Hold layer management - active when holding idle, deactivated during actions
        private int holdLayerIdx = -1;  // The active hold layer, -1 means no hold layer
        private bool isHoldLayerActive = false;  // True when hold layer should be at weight 1
        private bool isHoldLayerBlendingOut = false;  // True while blending hold layer to 0 for action

        // Network state
        private NetworkVariable<int> equippedItemId = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        #region Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            inventoryManager = GetComponent<IInventoryManager>();
            staminaManager = GetComponent<IStaminaManager>();
            playerController = GetComponent<PlayerController>();
            
            if (inputManager == null)
                inputManager = GetComponent<IInputManager>();

            if (playerAnimator == null)
                playerAnimator = GetComponentInChildren<Animator>();

            // Cache base animator controller for restoration on unequip
            if (playerAnimator != null)
                baseAnimatorController = playerAnimator.runtimeAnimatorController;

            equippedItemId.OnValueChanged += OnEquippedItemChanged;

            // Auto-add animation events receiver on the animator's GameObject
            if (playerAnimator != null)
            {
                var animatorGO = playerAnimator.gameObject;
                if (animatorGO.GetComponent<EquipmentAnimationEvents>() == null)
                {
                    animatorGO.AddComponent<EquipmentAnimationEvents>();
                }

                // Auto-add shooter IK controller + aim target provider on the animator's GameObject
                shooterIKController = animatorGO.GetComponent<ShooterIKController>();
                if (shooterIKController == null)
                {
                    shooterIKController = animatorGO.AddComponent<ShooterIKController>();
                }
                if (animatorGO.GetComponent<AimTargetProvider>() == null)
                {
                    animatorGO.AddComponent<AimTargetProvider>();
                }
                
                // Assign character IK data if available
                if (shooterIKController != null && characterIKData != null)
                {
                    shooterIKController.characterIKData = characterIKData;
                }
            }

            // Subscribe to quick inventory selection
            if (IsOwner && QuickInventoryUI.Instance != null)
            {
                QuickInventoryUI.Instance.OnSlotSelected += OnQuickSlotSelected;
            }

            // Sync fix: Listen to inventory changes directly to handle drop/unequip immediately
            // This prevents race conditions where RPC arrives before NetworkList update
            if (IsOwner && inventoryManager != null)
            {
                inventoryManager.Slots.OnListChanged += OnInventoryListChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            equippedItemId.OnValueChanged -= OnEquippedItemChanged;

            if (QuickInventoryUI.Instance != null)
                QuickInventoryUI.Instance.OnSlotSelected -= OnQuickSlotSelected;

            if (inventoryManager != null && inventoryManager.Slots != null)
            {
                inventoryManager.Slots.OnListChanged -= OnInventoryListChanged;
            }

            currentHandler?.Dispose();
            currentHandler = null;

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;

            HandleInput();
            HandleSwapDebounce();
            UpdateLayerWeight();
            currentHandler?.OnUpdate();
        }

        /// <summary>
        /// Handles the debounce timer for weapon swapping.
        /// </summary>
        private void HandleSwapDebounce()
        {
            if (!hasPendingSwap) return;

            swapDebounceTimer -= Time.deltaTime;
            if (swapDebounceTimer <= 0f)
            {
                hasPendingSwap = false;
                ExecuteSwap();
            }
        }

        /// <summary>
        /// Smoothly blends the upper body layer weight towards target.
        /// Uses the active layer index from the currently equipped item.
        /// </summary>
        private void UpdateLayerWeight()
        {
            if (playerAnimator == null) return;
            
            int layerIndex = activeLayerIndex >= 0 ? activeLayerIndex : upperBodyLayerIndex;
            
            // Don't fight with ComboAttackBehavior over the combo layer during attacks
            if (isInComboAttack && layerIndex == comboLayerIndex) return;
            
            float currentWeight = playerAnimator.GetLayerWeight(layerIndex);
            
            // Always blend towards target at the configured speed
            if (Mathf.Abs(currentWeight - targetLayerWeight) > 0.001f)
            {
                float newWeight = Mathf.MoveTowards(currentWeight, targetLayerWeight, layerBlendSpeed * Time.deltaTime);
                playerAnimator.SetLayerWeight(layerIndex, newWeight);
            }
            
            // Also smoothly blend action layer if it's separate
            if (actionLayerIdx >= 0 && actionLayerIdx != layerIndex)
            {
                if (isInAction)
                {
                    // Blend action layer towards 1
                    float actionWeight = playerAnimator.GetLayerWeight(actionLayerIdx);
                    if (Mathf.Abs(actionWeight - 1f) > 0.001f)
                    {
                        float newWeight = Mathf.MoveTowards(actionWeight, 1f, layerBlendSpeed * Time.deltaTime);
                        playerAnimator.SetLayerWeight(actionLayerIdx, newWeight);
                    }
                }
                else if (isActionBlendingOut)
                {
                    // Blend action layer towards 0
                    float actionWeight = playerAnimator.GetLayerWeight(actionLayerIdx);
                    if (actionWeight > 0.001f)
                    {
                        float newWeight = Mathf.MoveTowards(actionWeight, 0f, layerBlendSpeed * Time.deltaTime);
                        playerAnimator.SetLayerWeight(actionLayerIdx, newWeight);
                    }
                    else
                    {
                        // Blend complete - clean up
                        playerAnimator.SetLayerWeight(actionLayerIdx, 0f);
                        isActionBlendingOut = false;
                        actionLayerIdx = -1;

                        if (pendingBaseAnimatorRestore)
                        {
                            RestoreBaseAnimator();
                        }
                    }
                }
            }

            // Smoothly blend hold layer (inverse of action: on when idle, off during action)
            if (holdLayerIdx >= 0)
            {
                float holdTarget = isHoldLayerActive ? 1f : 0f;
                float holdWeight = playerAnimator.GetLayerWeight(holdLayerIdx);
                if (Mathf.Abs(holdWeight - holdTarget) > 0.001f)
                {
                    float newWeight = Mathf.MoveTowards(holdWeight, holdTarget, layerBlendSpeed * Time.deltaTime);
                    playerAnimator.SetLayerWeight(holdLayerIdx, newWeight);
                }
                else if (!isHoldLayerActive && !isHoldLayerBlendingOut && holdWeight < 0.001f)
                {
                    // Fully blended out and no longer needed
                    playerAnimator.SetLayerWeight(holdLayerIdx, 0f);
                }
            }
        }

        /// <summary>
        /// Determines the type of swap based on current and target states.
        /// </summary>
        private SwapType DetermineSwapType()
        {
            bool hasCurrentItem = currentHandler != null;
            var targetItem = inventoryManager?.GetItemAt(targetSlotIndex) ?? InventoryManager.InventoryItem.Empty;
            bool hasTargetItem = !targetItem.IsEmpty;
            
            if (hasCurrentItem && hasTargetItem) return SwapType.ItemToItem;
            if (hasCurrentItem && !hasTargetItem) return SwapType.ItemToEmpty;
            if (!hasCurrentItem && hasTargetItem) return SwapType.EmptyToItem;
            return SwapType.None;
        }

        /// <summary>
        /// Checks whether the target item in the target slot can be equipped to hand.
        /// Weapons, Tools, and Consumables always equip to hand.
        /// Resources only equip if equipSettings.canEquipToHand is explicitly true.
        /// </summary>
        private bool CanTargetItemEquipToHand()
        {
            if (inventoryManager == null) return false;
            
            var targetItem = inventoryManager.GetItemAt(targetSlotIndex);
            if (targetItem.IsEmpty) return false;
            
            var itemData = inventoryManager.GetItemData(targetItem.itemId);
            if (itemData == null) return false;
            
            return CanItemEquipToHand(itemData);
        }

        /// <summary>
        /// Checks whether the currently equipped item can be held in hand.
        /// Weapons, Tools, and Consumables always equip to hand.
        /// Resources only equip if equipSettings.canEquipToHand is explicitly true.
        /// </summary>
        private bool CanCurrentItemEquipToHand()
        {
            if (currentHandler == null) return false;
            
            if (inventoryManager == null || currentItemId < 0) return false;
            var itemData = inventoryManager.GetItemData(currentItemId);
            if (itemData == null) return false;
            
            return CanItemEquipToHand(itemData);
        }

        /// <summary>
        /// Central helper: determines whether an item can equip to hand based on its type.
        /// Weapons, Tools, Consumables → always true (they equip by default).
        /// Resources → driven by equipSettings.canEquipToHand (default false).
        /// </summary>
        private static bool CanItemEquipToHand(InventoryItemData itemData)
        {
            if (itemData == null) return false;
            
            // Weapons, Tools, Consumables always equip to hand
            if (itemData.objectType == ObjectType.Weapon ||
                itemData.objectType == ObjectType.Tools ||
                itemData.objectType == ObjectType.Consumable)
            {
                return true;
            }
            
            // Resources only equip if explicitly enabled
            return itemData.equipSettings?.canEquipToHand ?? false;
        }

        /// <summary>
        /// Gets the animator override controller for the target item.
        /// Data-driven: reads from equipSettings instead of type-specific data.
        /// </summary>
        private AnimatorOverrideController GetTargetOverrideController()
        {
            if (inventoryManager == null) return null;
            
            var targetItem = inventoryManager.GetItemAt(targetSlotIndex);
            if (targetItem.IsEmpty) return null;
            
            var itemData = inventoryManager.GetItemData(targetItem.itemId);
            if (itemData == null) return null;
            
            // equipSettings is the single source of truth for animator overrides
            return itemData.equipSettings?.animatorOverride;
        }

        /// <summary>
        /// Gets the animation layer index for the target item.
        /// Data-driven: reads from equipSettings.
        /// </summary>
        private int GetTargetAnimationLayerIndex()
        {
            if (inventoryManager == null) return upperBodyLayerIndex;
            
            var targetItem = inventoryManager.GetItemAt(targetSlotIndex);
            if (targetItem.IsEmpty) return upperBodyLayerIndex;
            
            var itemData = inventoryManager.GetItemData(targetItem.itemId);
            if (itemData == null) return upperBodyLayerIndex;
            
            // Data-driven: use equipSettings.animationLayerIndex
            return itemData.equipSettings?.animationLayerIndex ?? upperBodyLayerIndex;
        }


        /// <summary>
        /// Gets the ChangeID from the target item's changeID.
        /// This drives which state in the EquipItem sub-state machine plays.
        /// </summary>
        private int GetTargetChangeID()
        {
            if (inventoryManager == null) return 0;
            
            var targetItem = inventoryManager.GetItemAt(targetSlotIndex);
            if (targetItem.IsEmpty) return 0;
            
            var itemData = inventoryManager.GetItemData(targetItem.itemId);
            return itemData?.equipSettings?.changeID ?? 0;
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            if (currentHandler == null) return;
            if (inputManager == null) return;

            // Primary action (left click) - via InputManager for keybind support
            if (inputManager.PrimaryActionDown)
                currentHandler.OnPrimaryAction(true);
            if (inputManager.PrimaryActionUp)
                currentHandler.OnPrimaryAction(false);

            // Secondary action (right click) - only if inventory is closed
            if (!InventoryUI.Instance?.IsInventoryOpen ?? true)
            {
                if (inputManager.SecondaryActionDown)
                    currentHandler.OnSecondaryAction(true);
                if (inputManager.SecondaryActionUp)
                    currentHandler.OnSecondaryAction(false);
            }
        }

        private void OnQuickSlotSelected(int slotIndex)
        {
            RequestEquipFromSlot(slotIndex);
        }

        /// <summary>
        /// Requests a weapon swap to the specified slot with debouncing.
        /// Updates target immediately but delays animation execution.
        /// </summary>
        private void RequestEquipFromSlot(int slotIndex)
        {
            // If mid-animation, just update the target silently
            if (isSwapAnimationPlaying)
            {
                targetSlotIndex = slotIndex;
                hasPendingSwap = false;
                return;
            }

            // Normal case: start/reset debounce timer
            targetSlotIndex = slotIndex;
            swapDebounceTimer = SWAP_DEBOUNCE_DELAY;
            hasPendingSwap = true;
        }

        /// <summary>
        /// Executes the actual weapon swap after debounce timer expires.
        /// Uses single ChangeItem trigger - animation flow handles PutBack → GetItem transition.
        /// Animation events handle spawn/despawn at the right frames.
        /// </summary>
        private void ExecuteSwap()
        {
            // If target is same as current and item is equipped, skip
            if (targetSlotIndex == currentSlotIndex && currentHandler != null)
                return;

            // Determine what type of swap we're doing
            currentSwapType = DetermineSwapType();
            if (currentSwapType == SwapType.None)
                return;

            // Resources can't be held in hand — skip the pick animation entirely.
            // If neither the current nor target item can equip to hand, do a silent swap.
            bool currentCanEquip = CanCurrentItemEquipToHand();
            bool targetCanEquip = CanTargetItemEquipToHand();

            if (!currentCanEquip && !targetCanEquip)
            {
                // Silent resource-to-resource (or non-equippable-to-non-equippable) swap
                PerformSilentHandlerSwap();
                return;
            }

            isSwapAnimationPlaying = true;

            // Begin IK blend-out immediately so IK fades before hand reaches holster
            if (currentSwapType == SwapType.ItemToItem || currentSwapType == SwapType.ItemToEmpty)
            {
                shooterIKController?.BeginUnequipBlend();
            }
            
            // Set the active layer from the target item
            activeLayerIndex = GetTargetAnimationLayerIndex();
            int layerIndex = activeLayerIndex >= 0 ? activeLayerIndex : upperBodyLayerIndex;
            
            
            // Get ChangeID from target item's changeID (drives sub-state machine selection)
            int changeId = GetTargetChangeID();
            playerAnimator?.SetInteger("ChangeID", changeId);
            
            switch (currentSwapType)
            {
                case SwapType.ItemToItem:
                    // Has item, getting item - weight stays at 1
                    targetLayerWeight = 1f;
                    isBlendingLayerWeight = false;
                    PlayAnimation("ChangeItem");
                    if (IsOwner) SyncChangeItemAnimationRpc(changeId);
                    break;
                    
                case SwapType.ItemToEmpty:
                    // Has item, going empty - weight 1→0 (blend during GetItem)
                    // Start at 1, will blend to 0 in OnPutBackComplete
                    targetLayerWeight = 1f;
                    isBlendingLayerWeight = false;
                    PlayAnimation("ChangeItem");
                    if (IsOwner) SyncChangeItemAnimationRpc(changeId);
                    break;
                    
                case SwapType.EmptyToItem:
                    // Empty hand - apply new item's override first
                    var overrideController = GetTargetOverrideController();
                    if (overrideController != null)
                        SetAnimatorOverride(overrideController);
                    
                    // Start blending weight 0→1 throughout both animations
                    targetLayerWeight = 1f;
                    isBlendingLayerWeight = true;
                    PlayAnimation("ChangeItem");
                    if (IsOwner) SyncChangeItemAnimationRpc(changeId);
                    break;
            }
        }

        /// <summary>
        /// Silently swaps the current handler without playing any animation.
        /// Used when neither the current nor the target item can equip to hand (e.g. resource → resource).
        /// </summary>
        private void PerformSilentHandlerSwap()
        {
            // Cleanup old handler (no visual to despawn for resources)
            if (currentHandler != null)
            {
                currentHandler.OnUnequip();
                currentHandler.Dispose();
                currentHandler = null;
            }

            currentSlotIndex = -1;
            currentItemId = -1;

            // Equip new item silently
            if (inventoryManager == null) return;

            var item = inventoryManager.GetItemAt(targetSlotIndex);
            if (item.IsEmpty)
            {
                // Target is empty — just clear state
                if (IsOwner)
                {
                    equippedItemId.Value = -1;
                    UnequipItemRpc();
                }
                currentSwapType = SwapType.None;
                return;
            }

            var itemData = inventoryManager.GetItemData(item.itemId);
            if (itemData == null)
            {
                currentSwapType = SwapType.None;
                return;
            }

            currentHandler = CreateHandler(itemData.objectType, itemData);
            if (currentHandler != null)
            {
                currentSlotIndex = targetSlotIndex;
                currentItemId = item.itemId;
                currentHandler.OnEquip(this, itemData, targetSlotIndex);
            }

            // Sync to network
            if (IsOwner)
            {
                equippedItemId.Value = item.itemId;
                EquipItemRpc(item.itemId);
            }

            currentSwapType = SwapType.None;
        }

        /// <summary>
        /// Equips the target item without despawning current (used for EmptyToItem).
        /// </summary>
        private void PerformItemEquip()
        {
            if (inventoryManager == null) return;

            var item = inventoryManager.GetItemAt(targetSlotIndex);
            if (item.IsEmpty) return;

            var itemData = inventoryManager.GetItemData(item.itemId);
            if (itemData == null) return;

            // Create new handler and equip
            currentHandler = CreateHandler(itemData.objectType, itemData);
            if (currentHandler == null) return;

            currentSlotIndex = targetSlotIndex;
            currentItemId = item.itemId;

            currentHandler.OnEquip(this, itemData, targetSlotIndex);

            // Spawn new item visual
            if (currentHandler is BaseItemHandler newHandler)
            {
                newHandler.SpawnVisualModel();
            }

            // Sync to network
            if (IsOwner)
            {
                equippedItemId.Value = item.itemId;
                EquipItemRpc(item.itemId);
                SyncSpawnItemRpc();
            }
        }

        /// <summary>
        /// Single animation event for all swap scenarios.
        /// Called at the midpoint of the ChangeItem animation (when hand reaches holster).
        /// Handles: EmptyToItem (equip), ItemToItem (swap), ItemToEmpty (unequip).
        /// </summary>
        public void OnSwapComplete()
        {
            // Guard: Ignore duplicate calls (animation event may fire twice)
            if (currentSwapType == SwapType.None)
                return;
            // 1. Despawn current item (if any)
            if (currentHandler is BaseItemHandler oldHandler)
            {
                oldHandler.DespawnVisualModel();
                if (IsOwner) SyncDespawnItemRpc();
            }
            
            // 2. Cleanup old handler (if any)
            if (currentHandler != null)
            {
                currentHandler.OnUnequip();
                currentHandler.Dispose();
                currentHandler = null;
            }
            
            currentSlotIndex = -1;
            currentItemId = -1;
            
            int layerIndex = activeLayerIndex >= 0 ? activeLayerIndex : upperBodyLayerIndex;
            
            // 3. Handle based on swap type
            if (currentSwapType == SwapType.ItemToItem || currentSwapType == SwapType.EmptyToItem)
            {
                // Apply new item's override controller (for ItemToItem; EmptyToItem already applied in ExecuteSwap)
                if (currentSwapType == SwapType.ItemToItem)
                {
                    var overrideController = GetTargetOverrideController();
                    if (overrideController != null)
                        SetAnimatorOverride(overrideController);
                    else
                        RestoreBaseAnimator();
                }
                
                // Spawn new item
                PerformItemEquip();
                
                // Set layer weight to 1
                targetLayerWeight = 1f;
                playerAnimator.SetLayerWeight(layerIndex, 1f);

                // Activate hold layer if configured
                ActivateHoldLayer();

                // IK is activated by the weapon handlers themselves via OnVisualModelSpawned
                
                if (IsOwner) SyncSwapCompleteRpc(true);
            }
            else if (currentSwapType == SwapType.ItemToEmpty)
            {
                // No new item - restore base animator
                RestoreBaseAnimator();
                
                // Set layer weight to 0
                targetLayerWeight = 0f;
                playerAnimator.SetLayerWeight(layerIndex, 0f);
                activeLayerIndex = -1;
                
                // Deactivate weapon IK
                shooterIKController?.Deactivate();

                // Force-clean any active action/hold layers (no blend, item is going away)
                ForceEndAction();
                ForceEndHoldLayer();
                
                if (IsOwner)
                {
                    equippedItemId.Value = -1;
                    UnequipItemRpc();
                    SyncSwapCompleteRpc(false);
                }
            }
            
            // 4. Finalize swap state
            isSwapAnimationPlaying = false;
            isBlendingLayerWeight = false;
            currentSwapType = SwapType.None;
        }

        #endregion

        #region Unequip

        // Note: Equipping is handled by RequestEquipFromSlot() -> ExecuteSwap() via quick inventory selection.
        // Use empty slot selection to unequip with animation (SwapType.ItemToEmpty).

        /// <summary>
        /// Unequips the current item without playing animation.
        /// Used when auto-unequipping after consuming the last item or dropping the equipped item.
        /// For animated unequip, select an empty quick slot (SwapType.ItemToEmpty).
        /// </summary>
        public void UnequipSilent()
        {
            // Deactivate weapon IK
            shooterIKController?.Deactivate();

            // Check if we just finished an action and are currently blending it out
            bool isSmoothUnequip = isActionBlendingOut;

            // Force-clean any active action/hold layers
            if (!isSmoothUnequip)
            {
                ForceEndAction();
            }
            ForceEndHoldLayer();
            
            // Restore base animator (remove item-specific override controller)
            if (isSmoothUnequip)
            {
                pendingBaseAnimatorRestore = true;
            }
            else
            {
                RestoreBaseAnimator();
            }

            // Reset upper body layer weight to 0 (no item equipped)
            int layerIndex = activeLayerIndex >= 0 ? activeLayerIndex : upperBodyLayerIndex;
            targetLayerWeight = 0f;
            
            if (playerAnimator != null && !isSmoothUnequip)
            {
                playerAnimator.SetLayerWeight(layerIndex, 0f);
            }
            activeLayerIndex = -1;

            // Silent unequip - despawn and cleanup immediately
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.DespawnVisualModel();
                if (IsOwner) SyncDespawnItemRpc();
            }
            CleanupCurrentHandler();

            if (IsOwner)
            {
                equippedItemId.Value = -1;
                UnequipItemRpc();
            }
        }

        /// <summary>
        /// Handles the case when the equipped slot's contents change via inventory swap.
        /// Cleanly tears down the old item and re-equips with the new one using swap animation.
        /// If the slot is now empty, performs a silent unequip.
        /// </summary>
        public void HandleEquippedSlotChanged()
        {
            if (!IsOwner || currentSlotIndex < 0) return;
            if (inventoryManager == null) return;

            int equippedSlot = currentSlotIndex;
            var newItem = inventoryManager.GetItemAt(equippedSlot);

            // If the item in the slot is the same, nothing to do
            if (!newItem.IsEmpty && newItem.itemId == currentItemId) return;

            if (newItem.IsEmpty)
            {
                // Slot is now empty — unequip silently
                UnequipSilent();
            }
            else
            {
                // Slot has a different item — trigger a proper swap animation
                // Use the existing equip pipeline: set target to the same slot and force a re-equip
                targetSlotIndex = equippedSlot;

                // Clean up old item state first
                shooterIKController?.Deactivate();
                ForceEndAction();
                ForceEndHoldLayer();

                // Force currentSlotIndex to -1 so ExecuteSwap doesn't skip (same-slot guard)
                currentSlotIndex = -1;

                // Execute the swap — this will trigger the equip animation and handle everything
                ExecuteSwap();
            }
        }

        /// <summary>
        /// Handles low-level inventory list changes to catch drops/removals immediately.
        /// </summary>
        private void OnInventoryListChanged(NetworkListEvent<InventoryManager.InventoryItem> changeEvent)
        {
            // If the slot we are holding changed, re-evaluate immediately
            if (currentSlotIndex >= 0 && changeEvent.Index == currentSlotIndex)
            {
                HandleEquippedSlotChanged();
            }
        }

        /// <summary>
        /// Cleans up the current handler. Called after despawn.
        /// </summary>
        private void CleanupCurrentHandler()
        {

            if (currentHandler != null)
            {
                currentHandler.OnUnequip();
                currentHandler.Dispose();
                currentHandler = null;
            }
            currentSlotIndex = -1;
            currentItemId = -1;
        }

        /// <summary>
        /// Creates a handler for the given item.
        /// Weapons/Tools/Consumables get their type-specific handlers.
        /// Resources use SimpleEquipHandler if canEquipToHand is true, else ResourceHandler.
        /// </summary>
        private IItemHandler CreateHandler(ObjectType objectType, InventoryItemData itemData = null)
        {
            // Type-specific handlers for items with unique behavior
            return objectType switch
            {
                ObjectType.Weapon => itemData?.meleeWeaponData != null
                    ? new MeleeWeaponHandler()
                    : itemData?.chargedWeaponData != null
                        ? new BowWeaponHandler()
                    : itemData?.shooterWeaponData != null
                        ? (IItemHandler)new ShooterWeaponHandler()
                        : new SimpleEquipHandler(),
                ObjectType.Tools => new ToolHandler(),
                ObjectType.Consumable => new ConsumableHandler(),
                // Resources: use SimpleEquipHandler if canEquipToHand is true, else ResourceHandler
                ObjectType.Resource => CanItemEquipToHand(itemData) ? new SimpleEquipHandler() : new ResourceHandler(),
                _ => CanItemEquipToHand(itemData) ? new SimpleEquipHandler() : null
            };
        }

        #endregion

        #region Network Sync

        [Rpc(SendTo.NotMe)]
        private void EquipItemRpc(int itemId)
        {
            // cleanup potential old handler first
            if (currentHandler is BaseItemHandler oldHandler)
            {
                oldHandler.DespawnVisualModel();
            }
            currentHandler?.Dispose();
            
            // Remote players: show the equipped item
            var itemData = inventoryManager?.GetItemData(itemId);
            if (itemData == null) return;

            currentHandler = CreateHandler(itemData.objectType, itemData);
            currentHandler?.OnEquip(this, itemData, -1); // -1 for remote player
            currentItemId = itemId;
        }

        [Rpc(SendTo.NotMe)]
        private void UnequipItemRpc()
        {
            if (currentHandler is BaseItemHandler oldHandler)
            {
                oldHandler.DespawnVisualModel();
            }
            currentHandler?.OnUnequip();
            currentHandler?.Dispose();
            currentHandler = null;
            currentItemId = -1;
        }

        private void OnEquippedItemChanged(int previousValue, int newValue)
        {
            // Handle late-joining players
            if (!IsOwner && newValue != currentItemId)
            {
                // Cleanup old item
                 if (currentHandler is BaseItemHandler oldHandler)
                {
                    oldHandler.DespawnVisualModel();
                }
                currentHandler?.Dispose();
                currentHandler = null;

                if (newValue != -1)
                {
                    var itemData = inventoryManager?.GetItemData(newValue);
                    if (itemData != null)
                    {
                        currentHandler = CreateHandler(itemData.objectType, itemData);
                        currentHandler?.OnEquip(this, itemData, -1);
                        
                        // Local spawn for late joiners
                         if (currentHandler is BaseItemHandler newHandler)
                        {
                            newHandler.SpawnVisualModel();
                        }
                    }
                }
                currentItemId = newValue;
            }
        }

        /// <summary>
        /// RPC to sync primary action to other players.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        public void SyncPrimaryActionRpc(bool pressed)
        {
            currentHandler?.OnPrimaryAction(pressed);
        }

        /// <summary>
        /// RPC to sync secondary action to other players.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        public void SyncSecondaryActionRpc(bool pressed)
        {
            currentHandler?.OnSecondaryAction(pressed);
        }

        /// <summary>
        /// RPC to sync ChangeItem animation trigger to other players.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void SyncChangeItemAnimationRpc(int changeId)
        {
            isSwapAnimationPlaying = true;
            targetLayerWeight = 1f;
            playerAnimator?.SetInteger("ChangeID", changeId);
            PlayAnimation("ChangeItem");
        }

        /// <summary>
        /// RPC to sync swap completion to other players.
        /// Called by OnSwapComplete to sync the entire swap result.
        /// </summary>
        [Rpc(SendTo.NotMe)]
        private void SyncSwapCompleteRpc(bool hasNewItem)
        {
            if (hasNewItem)
            {
                // Remote player: spawn the new item
                PerformItemEquip();
            }
            
            // Finalize state
            isSwapAnimationPlaying = false;
            currentSwapType = SwapType.None;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the hold point for equipped items.
        /// </summary>
        public Transform GetHoldPoint() => rightHandHoldPoint;

        /// <summary>
        /// Gets the left hand hold point.
        /// </summary>
        public Transform GetLeftHandHoldPoint() => leftHandHoldPoint;

        /// <summary>
        /// Plays an animation trigger on the player animator.
        /// </summary>
        public void PlayAnimation(string triggerName)
        {
            if (playerAnimator != null && !string.IsNullOrEmpty(triggerName))
            {
                playerAnimator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// Cross-fades to an animation state for smooth transitions.
        /// Used by melee combo system for attack/exit transitions.
        /// </summary>
        public void PlayAnimationCrossFade(string stateName, float transitionDuration = 0.1f, int layer = 0)
        {
            if (playerAnimator != null && !string.IsNullOrEmpty(stateName))
            {
                playerAnimator.CrossFadeInFixedTime(stateName, transitionDuration, layer);
            }
        }

        /// <summary>
        /// Sets an animation bool parameter.
        /// </summary>
        public void SetAnimatorBool(string paramName, bool value)
        {
            if (playerAnimator != null && !string.IsNullOrEmpty(paramName))
            {
                playerAnimator.SetBool(paramName, value);
            }
        }

        /// <summary>
        /// Sets an animation override controller.
        /// </summary>
        public void SetAnimatorOverride(AnimatorOverrideController overrideController)
        {
            if (playerAnimator != null && overrideController != null)
            {
                playerAnimator.runtimeAnimatorController = overrideController;
                pendingBaseAnimatorRestore = false;
            }
        }

        /// <summary>
        /// Restores the base animator controller (called on unequip).
        /// </summary>
        public void RestoreBaseAnimator()
        {
            if (playerAnimator != null && baseAnimatorController != null)
            {
                playerAnimator.runtimeAnimatorController = baseAnimatorController;
                pendingBaseAnimatorRestore = false;
            }
        }

        /// <summary>
        /// Reduces durability of the item in the specified slot.
        /// </summary>
        public void ReduceItemDurability(int slotIndex, int amount)
        {
            inventoryManager?.ReduceDurabilityRpc(slotIndex, amount);
        }

        /// <summary>
        /// Gets the inventory manager reference.
        /// </summary>
        public IInventoryManager GetInventoryManager() => inventoryManager;

        /// <summary>
        /// Gets the stamina manager reference.
        /// </summary>
        public IStaminaManager GetStaminaManager() => staminaManager;

        /// <summary>
        /// Gets the PlayerController for movement control.
        /// </summary>
        public PlayerController GetPlayerController() => playerController;

        /// <summary>
        /// Gets the player animator for animation queries.
        /// </summary>
        public Animator GetAnimator() => playerAnimator;

        /// <summary>
        /// Gets the shooter IK controller for external access.
        /// </summary>
        public ShooterIKController GetShooterIKController() => shooterIKController;

        /// <summary>
        /// Gets the current equipped item data.
        /// </summary>
        public InventoryItemData GetCurrentItemData()
        {
            if (currentItemId == -1 || inventoryManager == null)
                return null;
            return inventoryManager.GetItemData(currentItemId);
        }

        /// <summary>
        /// Gets the current handler (for type-specific operations).
        /// </summary>
        public T GetCurrentHandler<T>() where T : class, IItemHandler
        {
            return currentHandler as T;
        }

        /// <summary>
        /// Spawns the visual model for the current item.
        /// Called by animation event at the right frame of equip animation.
        /// </summary>
        public void SpawnCurrentItem()
        {
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.SpawnVisualModel();
                
                if (IsOwner)
                {
                    SyncSpawnItemRpc();
                }
            }
        }

        /// <summary>
        /// Despawns the visual model for the current item.
        /// Called by animation event at the right frame of unequip animation.
        /// </summary>
        public void DespawnCurrentItem()
        {
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.DespawnVisualModel();
                
                if (IsOwner)
                {
                    SyncDespawnItemRpc();
                }
            }
            
            // Cleanup handler after despawning (for animation event flow)
            CleanupCurrentHandler();
        }

        [Rpc(SendTo.NotMe)]
        private void SyncSpawnItemRpc()
        {
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.SpawnVisualModel();
            }
        }

        [Rpc(SendTo.NotMe)]
        private void SyncDespawnItemRpc()
        {
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.DespawnVisualModel();
            }
        }

        /// <summary>
        /// Called by animation event when consume animation reaches the consume point.
        /// Single event that handles everything: apply effects, reduce inventory, despawn and unequip.
        /// </summary>
        public void OnConsumeAnimationComplete()
        {
            // Only apply to consumables, not tools or weapons
            if (currentHandler is not ConsumableHandler consumableHandler)
                return;
            
            // 1. Apply effects and consume from inventory via handler
            consumableHandler.OnConsumeAnimationEvent();
            
            // 2. Despawn the visual model
            if (currentHandler is BaseItemHandler baseHandler)
            {
                baseHandler.DespawnVisualModel();
                
                if (IsOwner)
                {
                    SyncDespawnItemRpc();
                }
            }
            
            // 3. Clean up handler after the consume animation completes
            CleanupCurrentHandler();
            
            if (IsOwner)
            {
                equippedItemId.Value = -1;
                UnequipItemRpc();
            }
        }


        /// <summary>
        /// Called by animation event when blade hit detection should start.
        /// Enables continuous blade hitbox detection.
        /// </summary>
        public void OnBladeEnable() => currentHandler?.OnBladeEnable();

        /// <summary>
        /// Called by animation event when blade hit detection should stop.
        /// Disables continuous blade hitbox detection.
        /// </summary>
        public void OnBladeDisable() => currentHandler?.OnBladeDisable();

        public void OnComboWindowStart() => currentHandler?.OnComboWindowStart();
        public void OnComboWindowEnd() => currentHandler?.OnComboWindowEnd();
        public void OnAttackExitEnd() => currentHandler?.OnAttackExitEnd();

        /// <summary>
        /// Whether a combo attack is currently in progress.
        /// Used by ComboAttackBehavior to avoid resetting layer weight mid-combo.
        /// </summary>
        public bool IsInComboAttack => isInComboAttack;

        /// <summary>
        /// Called by MeleeWeaponHandler when a combo attack starts.
        /// Prevents UpdateLayerWeight from interfering with ComboAttackBehavior's layer control.
        /// </summary>
        public void OnComboAttackStarted(int layerIndex)
        {
            isInComboAttack = true;
            comboLayerIndex = layerIndex;
        }

        /// <summary>
        /// Called by MeleeWeaponHandler when the combo attack ends.
        /// Re-enables UpdateLayerWeight control of the layer.
        /// </summary>
        public void OnComboAttackEnded()
        {
            isInComboAttack = false;
            comboLayerIndex = -1;
        }

        /// <summary>
        /// Called by handlers when a tool/consumable action starts.
        /// If actionLayerIndex is different from equipLayerIndex, sets the action layer weight to 1.
        /// If same layer, does nothing (equip system already owns the weight).
        /// </summary>
        public void OnActionStarted()
        {
            if (playerAnimator == null || currentItemId < 0) return;

            var itemData = inventoryManager?.GetItemData(currentItemId);
            if (itemData?.equipSettings == null) return;

            int equipLayer = itemData.equipSettings.animationLayerIndex;
            int actionLayer = itemData.equipSettings.actionLayerIndex;

            // -1 means same as equip layer — no separate management needed
            if (actionLayer < 0 || actionLayer == equipLayer)
            {
                actionLayerIdx = -1;
                isInAction = false;
                return;
            }

            // Different layer — start managing it (smooth blend via UpdateLayerWeight)
            actionLayerIdx = actionLayer;
            isInAction = true;
            isActionBlendingOut = false;  // Cancel any ongoing blend-out

            // Deactivate hold layer during action (smooth blend out)
            if (holdLayerIdx >= 0)
            {
                isHoldLayerActive = false;
                isHoldLayerBlendingOut = true;
            }
        }

        /// <summary>
        /// Called by handlers/animation events when a tool/consumable action ends.
        /// If actionLayerIndex was different from equipLayerIndex, resets the action layer weight to 0.
        /// If same layer, does nothing.
        /// </summary>
        public void OnActionEnded()
        {
            if (!isInAction || actionLayerIdx < 0) return;

            // Start smooth blend-out via UpdateLayerWeight
            isInAction = false;
            isActionBlendingOut = true;

            // Re-activate hold layer after action ends (smooth blend in)
            if (holdLayerIdx >= 0)
            {
                isHoldLayerActive = true;
                isHoldLayerBlendingOut = false;
            }
        }

        /// <summary>
        /// Force-resets the action layer immediately (no smooth blend).
        /// Used during unequip when the item is being removed entirely.
        /// </summary>
        private void ForceEndAction()
        {
            if (actionLayerIdx >= 0 && playerAnimator != null)
            {
                playerAnimator.SetLayerWeight(actionLayerIdx, 0f);
            }

            isInAction = false;
            isActionBlendingOut = false;
            actionLayerIdx = -1;
        }

        /// <summary>
        /// Activates the hold layer for the current item (reads from equipSettings).
        /// Called when an item is equipped.
        /// When holdLayerIndex is -1 (no hold pose), fades the equip layer back to 0
        /// so it doesn't stay active and mask action layers.
        /// </summary>
        private void ActivateHoldLayer()
        {
            if (playerAnimator == null || currentItemId < 0) return;

            var itemData = inventoryManager?.GetItemData(currentItemId);
            int holdLayer = itemData?.equipSettings?.holdLayerIndex ?? -1;

            if (holdLayer < 0)
            {
                holdLayerIdx = -1;
                isHoldLayerActive = false;

                // No hold layer — fade the equip layer back to 0 after equip animation.
                // Without this, the equip layer stays at weight 1 forever, which can
                // mask action layers (e.g. consume on layer 2) and waste layer weight.
                targetLayerWeight = 0f;
                return;
            }

            holdLayerIdx = holdLayer;
            isHoldLayerActive = true;
            isHoldLayerBlendingOut = false;
        }

        /// <summary>
        /// Force-resets the hold layer immediately (no smooth blend).
        /// Used during unequip when the item is being removed entirely.
        /// </summary>
        private void ForceEndHoldLayer()
        {
            if (holdLayerIdx >= 0 && playerAnimator != null)
            {
                playerAnimator.SetLayerWeight(holdLayerIdx, 0f);
            }

            holdLayerIdx = -1;
            isHoldLayerActive = false;
            isHoldLayerBlendingOut = false;
        }

        /// <summary>
        /// Called by animation event when reload animation completes.
        /// Routes to the current shooter handler.
        /// </summary>
        public void OnReloadComplete() => currentHandler?.OnReloadComplete();
        public void OnGrabArrow() => currentHandler?.OnGrabArrow();
        public void OnNockArrow() => currentHandler?.OnNockArrow();

        // Note: IK activation is now handled by the weapon handlers themselves
        // in OnVisualModelSpawned(). They call ShooterIKController.Activate() directly
        // with the weapon category and grip/aim transforms from the prefab.

        /// <summary>
        /// Server RPC to spawn a projectile. Called by ShooterWeaponHandler on the owning client.
        /// The server instantiates the prefab, sets velocity, initializes damage, and spawns as NetworkObject.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void FireProjectileRpc(ulong ownerClientId, Vector3 spawnPos, Vector3 direction, float speed, float damage, int damageType, int tier)
        {
            Debug.Log($"[EquipmentController] Server received FireProjectileRpc. Owner: {ownerClientId}, damage: {damage}, speed: {speed}");
            // We need to get the projectile prefab from the currently equipped visual's ShooterWeapon component
            if (currentHandler is BaseItemHandler baseHandler && baseHandler.EquippedObject != null)
            {
                var shooterComponent = baseHandler.EquippedObject.GetComponent<ShooterWeapon>();
                var bowComponent = baseHandler.EquippedObject.GetComponent<BowWeapon>();

                GameObject prefab = null;
                GameObject hitFx = null;
                AudioClip hitSnd = null;

                if (shooterComponent != null && shooterComponent.projectilePrefab != null)
                {
                    prefab = shooterComponent.projectilePrefab;
                    hitFx = shooterComponent.hitEffectPrefab;
                    hitSnd = shooterComponent.hitSound;
                    Debug.Log($"[EquipmentController] Found projectilePrefab on ShooterWeapon: {prefab.name}");
                }
                else if (bowComponent != null && bowComponent.projectilePrefab != null)
                {
                    prefab = bowComponent.projectilePrefab;
                    hitFx = bowComponent.hitEffectPrefab;
                    hitSnd = bowComponent.hitSound;
                    Debug.Log($"[EquipmentController] Found projectilePrefab on BowWeapon: {prefab.name}");
                }

                if (prefab != null)
                {
                    // Instantiate and orient the projectile
                    var projectileObj = Instantiate(prefab, spawnPos, Quaternion.LookRotation(direction));

                    // Spawn as NetworkObject so all clients can see it
                    var netObj = projectileObj.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn();
                        Debug.Log($"[EquipmentController] Successfully spawned projectile NetworkObject with ID: {netObj.NetworkObjectId}");
                    }
                    else
                    {
                        Debug.LogError("[EquipmentController] FireProjectileRpc: Projectile prefab missing NetworkObject.");
                        Destroy(projectileObj);
                        return;
                    }

                    // Initialize damage data with weapon-specific hit overrides
                    var projectile = projectileObj.GetComponent<Projectile>();
                    if (projectile != null)
                    {
                        projectile.Initialize(
                            ownerClientId, damage, (DamageType)damageType, tier,
                            hitFx, hitSnd
                        );
                    }

                    // Set velocity
                    var rb = projectileObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.useGravity = false;
                        rb.linearVelocity = direction.normalized * speed;
                    }
                    
                    return;
                }
            }
            
            Debug.LogWarning("[EquipmentController] FireProjectileRpc: No ShooterWeapon component or projectile prefab found on current weapon.");
        }

        /// <summary>
        /// Server RPC to apply melee damage to a target. Called by MeleeWeaponHandler on the owning client.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ApplyMeleeDamageServerRpc(ulong targetNetworkObjectId, float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            Debug.Log($"[EquipmentController] Server received ApplyMeleeDamageServerRpc. TargetNetworkObjectId: {targetNetworkObjectId}, damage: {damageAmount}");
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            {
                Debug.LogWarning("[EquipmentController] ApplyMeleeDamageServerRpc failed: NetworkManager or SpawnManager is null.");
                return;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
            {
                Debug.Log($"[EquipmentController] Found target NetworkObject: {targetNetObj.name}");
                var damageable = targetNetObj.GetComponentInParent<IDamageable>();
                Debug.Log($"[EquipmentController] Target damageable component exists: {damageable != null}, IsAlive: {damageable?.IsAlive}");
                if (damageable != null && damageable.IsAlive)
                {
                    DamageInfo damageInfo = new DamageInfo(
                        damageAmount,
                        DamageType.Physical,
                        hitPoint,
                        NetworkObjectId
                    ).WithDirection(hitDirection);

                    Debug.Log($"[EquipmentController] Calling TakeDamage on target {targetNetObj.name} with damage amount {damageAmount}.");
                    damageable.TakeDamage(damageInfo);
                }
            }
            else
            {
                Debug.LogWarning($"[EquipmentController] ApplyMeleeDamageServerRpc failed: Network ID {targetNetworkObjectId} was not found in SpawnedObjects.");
            }
        }

        #endregion
    }
}