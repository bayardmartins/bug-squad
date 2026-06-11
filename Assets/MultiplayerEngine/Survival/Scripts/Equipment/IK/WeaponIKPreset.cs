using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Serializable class containing IK offset data for a weapon category.
    /// Each weapon category (e.g., "Rifle", "Bow", "Pistol") gets its own preset
    /// with distinct IK poses for idle and aiming states.
    /// 
    /// These are embedded into CharacterIKData.
    /// </summary>
    [System.Serializable]
    public class WeaponIKPreset
    {
        [Header("Weapon Item")]
        [Tooltip("The InventoryItemData this IK preset applies to")]
        public InventoryItemData itemData;

        [Header("Hand Configuration")]
        [Tooltip("If true, right hand is weapon hand. If false, left hand is weapon hand.")]
        public bool isRightHandPrimary = true;

        [Tooltip("If true, the secondary (support) hand will use IK to grip the weapon.")]
        public bool useSecondaryHand = true;

        [Header("Idle Pose (Standing, Not Aiming)")]
        public IKAdjust idle = new IKAdjust();

        [Header("Aiming Pose")]
        public IKAdjust aiming = new IKAdjust();

        /// <summary>
        /// Returns the IKAdjust for the current state.
        /// </summary>
        public IKAdjust GetAdjust(bool isAiming)
        {
            return isAiming ? aiming : idle;
        }
    }
}