using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// ScriptableObject containing shooter weapon configuration.
    /// Used for normal fire-on-click guns (rifles, pistols, etc.).
    /// Charge-based weapons (bows, etc.) use ChargedWeaponData instead.
    /// Embedded as a sub-asset on InventoryItemData via ItemDatabaseWindow.
    /// </summary>
    // [CreateAssetMenu] removed as this is created via ItemDatabaseWindow
    public class ShooterWeaponData : ScriptableObject
    {
        [Header("Ammo")]
        [Tooltip("Item ID of the ammo resource in inventory (must be a Resource-type item)")]
        public int ammoItemId = -1;

        [Tooltip("Maximum rounds in the magazine")]
        public int magazineSize = 30;

        [Tooltip("Time in seconds to complete a reload")]
        public float reloadTime = 2f;

        [Header("Aim Alignment")]
        [Tooltip("Procedurally rotate upper arm to point at aim target")]
        public bool alignArmToAim = true;
        [Tooltip("Procedurally rotate hand to align weapon with aim target")]
        public bool alignHandToAim = true;

        [Header("Weapon Stats")]
        [Tooltip("Shots per second")]
        public float fireRate = 5f;

        [Tooltip("Damage per shot")]
        public float baseDamage = 15f;

        /// <summary>
        /// Gets the fire interval in seconds.
        /// </summary>
        public float FireInterval => fireRate > 0f ? 1f / fireRate : 0.2f;
    }
}
