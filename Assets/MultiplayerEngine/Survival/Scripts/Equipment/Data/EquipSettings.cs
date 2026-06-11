using UnityEngine;
using UnityEngine.Serialization;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Data-driven settings for item equip behavior.
    /// Note: Hand prefab uses ItemData.localPrefab, hand offsets use ItemData.handOffsetData.
    /// </summary>
    [System.Serializable]
    public class EquipSettings
    {
        [Tooltip("If true, this item can be equipped to the hand. Only used for Resources — Weapons, Tools, and Consumables always equip to hand.")]
        public bool canEquipToHand = false;

        [FormerlySerializedAs("equipAnimationID")]
        [Tooltip("Drives which equip/swap animation plays (ChangeID parameter in animator).")]
        public int changeID;

        [Tooltip("Variation within the action type (ActionID parameter in animator, e.g. different swing styles).")]
        public int actionID;

        [Tooltip("Index of the animator layer to apply this item's animations on (default 1).")]
        public int animationLayerIndex = 1;

        [Tooltip("Animator layer for action animations (e.g. tool swing, consume). -1 means same as equip layer (no separate action layer management).")]
        public int actionLayerIndex = -1;

        [Tooltip("Animator layer for hold/idle pose (active when holding item, deactivated during actions). -1 means no hold layer.")]
        public int holdLayerIndex = -1;

        [Tooltip("Animator override controller for this item's animations.")]
        public AnimatorOverrideController animatorOverride;
    }
}

