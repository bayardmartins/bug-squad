using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Receives animation events for equipment spawn/despawn.
    /// Attach this to the character with the Animator.
    /// Add animation events in your Equip/UnEquip clips that call these methods.
    /// </summary>
    public class EquipmentAnimationEvents : MonoBehaviour
    {
        private EquipmentController equipmentController;

        private void Awake()
        {
            equipmentController = GetComponentInParent<EquipmentController>();
            if (equipmentController == null)
                equipmentController = GetComponent<EquipmentController>();
        }

        /// <summary>
        /// Called by animation event when it's time to show the weapon/tool.
        /// Add this event to your Equip animation at the frame where hand grabs weapon.
        /// </summary>
        public void OnEquipItem()
        {
            equipmentController?.SpawnCurrentItem();
        }

        /// <summary>
        /// Called by animation event when it's time to hide the weapon/tool.
        /// Add this event to your UnEquip animation at the frame where weapon is holstered.
        /// </summary>
        public void OnUnequipItem()
        {
            equipmentController?.DespawnCurrentItem();
        }

        /// <summary>
        /// Alternative method name for compatibility.
        /// </summary>
        public void OnDrawWeapon() => OnEquipItem();

        /// <summary>
        /// Alternative method name for compatibility.
        /// </summary>
        public void OnHolsterWeapon() => OnUnequipItem();

        /// <summary>
        /// Called by animation event when it's time to consume the item.
        /// Single event that handles everything: apply effects, reduce inventory count, hide and despawn item, unequip.
        /// Add this event to your Consume/Drink animation at the frame where the item is consumed.
        /// </summary>
        public void OnConsumeItem()
        {
            equipmentController?.OnConsumeAnimationComplete();
        }

        /// <summary>
        /// Called by animation event at the midpoint of ChangeItem animation.
        /// Single event handles all swap scenarios: equip, swap, unequip.
        /// Add this event to your ChangeItem animation where the hand reaches the holster.
        /// </summary>
        public void OnSwapComplete()
        {
            equipmentController?.OnSwapComplete();
        }

        /// <summary>
        /// Alias for OnConsumeItem - kept for backwards compatibility.
        /// Use OnConsumeItem instead.
        /// </summary>
        public void OnUseConsumable() => OnConsumeItem();

        /// <summary>
        /// Called by animation event when blade hit detection should start.
        /// Add this event to your attack/tool animation at the start of the swing.
        /// </summary>
        public void BladeEnable()
        {
            equipmentController?.OnBladeEnable();
        }

        /// <summary>
        /// Called by animation event when blade hit detection should stop.
        /// Add this event to your attack/tool animation at the end of the swing.
        /// </summary>
        public void BladeDisable()
        {
            equipmentController?.OnBladeDisable();
        }

        // Combo window events are handled by ComboAttackBehavior (StateMachineBehaviour)
        // Only blade events remain as animation events on clips

        /// <summary>
        /// Called by animation event when reload animation completes.
        /// Add this event to your Reload animation at the frame where the magazine is inserted.
        /// </summary>
        public void OnReloadComplete()
        {
            equipmentController?.OnReloadComplete();
        }

        /// <summary>
        /// Called by animation event when the draw hand reaches the quiver to grab an arrow.
        /// Add this event to your bow draw animation at the frame where the hand reaches back.
        /// </summary>
        public void OnGrabArrow()
        {
            equipmentController?.OnGrabArrow();
        }

        /// <summary>
        /// Called by animation event when the hand brings the arrow to the bow string nock point.
        /// Add this event to your bow draw animation at the frame where the arrow meets the string.
        /// </summary>
        public void OnNockArrow()
        {
            equipmentController?.OnNockArrow();
        }
    }
}