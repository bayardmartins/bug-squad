using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for consumable items (potions, food, etc.).
    /// Uses item on primary action, applies effects to PlayerStatsManager, and reduces count.
    /// </summary>
    public class ConsumableHandler : BaseItemHandler
    {
        private ConsumableData consumableData;
        private IPlayerStatsManager statsManager;
        private float lastUseTime;
        private bool isConsuming;



        protected override void OnEquipInternal()
        {
            consumableData = itemData?.consumableData;
            statsManager = controller.GetComponent<IPlayerStatsManager>();
            isConsuming = false;

            // Pre-set ItemID so click only needs to trigger IsAction
            SetActionParameters();
        }

        protected override void OnUnequipInternal()
        {
            isConsuming = false;
        }

        public override void OnPrimaryAction(bool pressed)
        {
            if (!pressed) return;
            if (!CanUse()) return;

            StartConsume();
        }



        private bool CanUse()
        {
            // Don't allow use if already consuming
            if (isConsuming) return false;
            // Default 0.5s cooldown between uses
            return Time.time >= lastUseTime + 0.5f;
        }

        /// <summary>
        /// Starts the consume action - plays animation and waits for animation event.
        /// </summary>
        private void StartConsume()
        {
            lastUseTime = Time.time;
            isConsuming = true;

            // Trigger action animation - ItemID already set on equip, just trigger IsAction
            TriggerAction();

            // Play consume sound
            PlayConsumeSound();

            // Sync animation to other players
            if (controller != null && controller.IsOwner)
                controller.SyncPrimaryActionRpc(true);
        }

        /// <summary>
        /// Called by single animation event (OnConsumeItem) when the consumable should be consumed.
        /// Handles everything: apply effects, reduce inventory count, hide and despawn item.
        /// </summary>
        public void OnConsumeAnimationEvent()
        {
            if (!isConsuming) return;
            isConsuming = false;

            // 1. Apply consumable effects (health, stamina, etc.)
            ApplyEffect();

            // 2. Remove one item from inventory (via server RPC)
            ConsumeOneItem();

            // 3. Hide the consumable item in hand
            HideEquippedItem();

            // 4. Action complete - reset action layer weight if it's a separate layer
            EndAction();
        }

        private void HideEquippedItem()
        {
            if (equippedObject != null)
            {
                equippedObject.SetActive(false);
            }
        }

        private void ApplyEffect()
        {
            if (consumableData == null || statsManager == null) return;

            // Only apply if there are actual effects
            if (!consumableData.HasEffect) return;

            // Send to server to apply stats
            statsManager.RestoreStatsRpc(
                consumableData.healthRestore,
                consumableData.staminaRestore,
                consumableData.foodRestore,
                consumableData.waterRestore
            );

            // Spawn consume effect locally
            if (consumableData.consumeEffect != null && equippedObject != null)
            {
                var effect = Object.Instantiate(consumableData.consumeEffect, 
                    equippedObject.transform.position, Quaternion.identity);
                Object.Destroy(effect, 2f);
            }
        }

        private void ConsumeOneItem()
        {
            if (controller == null || inventorySlot < 0) return;

            var inventory = controller.GetInventoryManager();
            if (inventory == null) return;

            // Use the inventory's consume method (removes 1 item)
            inventory.ConsumeItemRpc(inventorySlot, 1);
        }

        private void PlayConsumeSound()
        {
            if (consumableData?.consumeSound != null && equippedObject != null)
            {
                AudioPool.Play(consumableData.consumeSound, equippedObject.transform.position);
            }
        }
    }
}