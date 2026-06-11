using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Defines the effects of a consumable item.
    /// Attach to InventoryItemData.consumableData for consumables.
    /// </summary>
    public class ConsumableData : ScriptableObject
    {
        [Header("Stat Restoration")]
        [Tooltip("Health points restored")]
        public float healthRestore = 0f;

        [Tooltip("Stamina points restored")]
        public float staminaRestore = 0f;

        [Tooltip("Food points restored")]
        public float foodRestore = 0f;

        [Tooltip("Water points restored")]
        public float waterRestore = 0f;

        [Header("Effects")]
        [Tooltip("Sound to play when consumed")]
        public AudioClip consumeSound;

        [Tooltip("Particle effect on consume")]
        public GameObject consumeEffect;

        /// <summary>
        /// Returns true if this consumable restores any stat.
        /// </summary>
        public bool HasEffect => healthRestore > 0 || staminaRestore > 0 || foodRestore > 0 || waterRestore > 0;
    }
}
