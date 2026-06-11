using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Abstract base class for all item handlers.
    /// Provides common functionality for equipping, unequipping, and managing equipped items.
    /// </summary>
    public abstract class BaseItemHandler : IItemHandler
    {
        // References
        protected EquipmentController controller;
        protected InventoryItemData itemData;
        protected int inventorySlot;
        protected GameObject equippedObject;
        
        // State
        protected bool isEquipped;
        protected bool isPerformingAction;  // True while an action animation is playing

        // Properties
        public virtual bool HasVisualModel => true;
        public bool IsEquipped => isEquipped;
        public bool IsPerformingAction => isPerformingAction;

        #region IItemHandler Implementation

        public virtual void OnEquip(EquipmentController controller, InventoryItemData itemData, int inventorySlot)
        {
            this.controller = controller;
            this.itemData = itemData;
            this.inventorySlot = inventorySlot;
            this.isEquipped = true;

            // Note: Visual model spawning is deferred to animation event
            // Call SpawnVisualModel() via animation event at the right frame

            OnEquipInternal();
        }

        public virtual void OnUnequip()
        {
            OnUnequipInternal();

            // Reset animator parameters to defaults before losing controller reference
            ClearActionParameters();

            // Note: Visual model despawning is deferred to animation event
            // Call DespawnVisualModel() via animation event at the right frame

            isEquipped = false;
            isPerformingAction = false;  // Clear action lock on unequip
            controller = null;
            itemData = null;
        }

        public virtual void OnPrimaryAction(bool pressed)
        {
            // Override in derived classes
        }

        public virtual void OnSecondaryAction(bool pressed)
        {
            // Override in derived classes
        }

        public virtual void OnUpdate()
        {
            // Override in derived classes for per-frame logic
        }

        public virtual void OnBladeEnable() { }
        public virtual void OnBladeDisable() { }
        public virtual void OnComboWindowStart() { }
        public virtual void OnComboWindowEnd() { }
        public virtual void OnAttackExitEnd() { }
        public virtual void OnReloadComplete() { }
        public virtual void OnGrabArrow() { }
        public virtual void OnNockArrow() { }

        public virtual void Dispose()
        {
            if (isEquipped)
                OnUnequip();
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Called after equip setup is complete. Override for type-specific init.
        /// </summary>
        protected virtual void OnEquipInternal() { }

        /// <summary>
        /// Called before unequip cleanup. Override for type-specific cleanup.
        /// </summary>
        protected virtual void OnUnequipInternal() { }

        /// <summary>
        /// Spawns the visual model and parents it to the hold point.
        /// </summary>
        protected virtual void SpawnEquippedObject()
        {
            if (itemData.localPrefab == null || controller == null)
                return;

            Transform holdPoint = controller.GetHoldPoint();
            equippedObject = Object.Instantiate(itemData.localPrefab, holdPoint);
            
            // Apply hand offsets: purely from per-item data (InventoryItemData)
            HandOffsetData activeHandOffset = itemData.handOffsetData;

            if (activeHandOffset != null)
            {
                activeHandOffset.ApplyTo(equippedObject.transform);
            }
            else
            {
                equippedObject.transform.localPosition = Vector3.zero;
                equippedObject.transform.localRotation = Quaternion.identity;
            }
            
            equippedObject.SetActive(true);
        }

        public GameObject EquippedObject => equippedObject;

        /// <summary>
        /// Public method to spawn the visual model.
        /// Called by animation event at the right frame of equip animation.
        /// </summary>
        public void SpawnVisualModel()
        {
            if (equippedObject != null) return; // Already spawned
            
            if (HasVisualModel && itemData?.localPrefab != null)
            {
                SpawnEquippedObject();
                OnVisualModelSpawned();
            }
        }

        /// <summary>
        /// Called after the visual model has been spawned.
        /// Override in handlers that need access to the equipped object (e.g., for IK setup).
        /// </summary>
        protected virtual void OnVisualModelSpawned()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Public method to despawn the visual model.
        /// Called by animation event at the right frame of unequip animation.
        /// </summary>
        public void DespawnVisualModel()
        {
            if (equippedObject != null)
            {
                Object.Destroy(equippedObject);
                equippedObject = null;
            }
        }

        /// <summary>
        /// Reduces durability of the equipped item.
        /// </summary>
        protected void ReduceDurability(int amount = 1)
        {
            if (controller != null && inventorySlot >= 0)
            {
                controller.ReduceItemDurability(inventorySlot, amount);
            }
        }

        /// <summary>
        /// Sets the ActionID and ItemID animator parameters based on the equipped item.
        /// Called automatically on equip so action animations are ready to trigger instantly.
        /// ActionID: groups items sharing the same animation (-1 = no action animation).
        /// ItemID: unique item identity (from itemData.itemId).
        /// </summary>
        protected void SetActionParameters()
        {
            var animator = controller?.GetAnimator();
            if (animator == null) return;

            animator.SetInteger("ActionID", itemData?.equipSettings?.actionID ?? -1);
            animator.SetInteger("ItemID", itemData?.itemId ?? 0);
        }

        /// <summary>
        /// Resets ActionID and ItemID animator parameters to defaults.
        /// Called on unequip so stale values don't persist after dropping or switching to empty.
        /// </summary>
        protected void ClearActionParameters()
        {
            var animator = controller?.GetAnimator();
            if (animator == null) return;

            animator.SetInteger("ActionID", -1);
            animator.SetInteger("ItemID", 0);
        }

        /// <summary>
        /// Triggers an action animation using the IsAction trigger.
        /// ItemID should already be set via SetActionParameters() on equip.
        /// Also notifies the controller to manage action layer weight if needed.
        /// </summary>
        /// <param name="force">If true, bypasses the isPerformingAction guard (used by combo systems).</param>
        /// <param name="suppressLayerControl">If true, prevents the controller from fading out the hold layer (used for shooting).</param>
        protected void TriggerAction(bool force = false, bool suppressLayerControl = false)
        {
            // Block if action already in progress (unless forced for combos)
            if (!force && isPerformingAction) return;

            var animator = controller?.GetAnimator();
            if (animator == null) return;

            isPerformingAction = true;

            // Notify controller to set action layer weight (if different from equip layer and not suppressed)
            if (!suppressLayerControl)
            {
                controller.OnActionStarted();
            }

            animator.SetTrigger("IsAction");
        }

        /// <summary>
        /// Call when the action animation/logic completes.
        /// Notifies controller to reset action layer weight (if different from equip layer).
        /// </summary>
        protected void EndAction()
        {
            isPerformingAction = false;
            controller?.OnActionEnded();
        }

        /// <summary>
        /// Triggers an animation on the player's animator.
        /// </summary>
        protected void PlayAnimation(string triggerName)
        {
            controller?.PlayAnimation(triggerName);
        }

        #endregion
    }
}
