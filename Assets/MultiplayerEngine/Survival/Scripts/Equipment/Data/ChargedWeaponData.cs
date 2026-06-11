using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// ScriptableObject containing charged weapon configuration.
    /// Used for bows, crossbows, and other hold-to-charge weapons.
    /// Embedded as a sub-asset on InventoryItemData via ItemDatabaseWindow.
    /// </summary>
    // [CreateAssetMenu] removed as this is created via ItemDatabaseWindow
    public class ChargedWeaponData : ScriptableObject
    {
        [Header("Ammo")]
        [Tooltip("Item ID of the ammo resource in inventory (e.g. arrows)")]
        public int ammoItemId = -1;

        [Header("Draw / Charge Mechanics")]
        [Tooltip("Seconds to reach full charge/draw")]
        public float chargeTime = 1.2f;

        [Tooltip("How far the string/mechanism pulls back (local Z units). Used by BowWeapon.")]
        public float drawDistance = 0.3f;

        [Header("Damage")]
        [Tooltip("Damage at zero/minimum charge")]
        public float minDamage = 5f;

        [Tooltip("Damage at full charge")]
        public float maxDamage = 40f;

        [Header("Projectile Speed")]
        [Tooltip("Projectile speed at minimum charge")]
        public float minProjectileSpeed = 10f;

        [Tooltip("Projectile speed at full charge")]
        public float maxProjectileSpeed = 40f;

        [Header("Aim Alignment")]
        [Tooltip("Procedurally rotate upper arm to point at aim target")]
        public bool alignArmToAim = true;

        [Tooltip("Procedurally rotate hand to align weapon with aim target")]
        public bool alignHandToAim = true;

        /// <summary>
        /// Calculates damage based on charge percentage (0-1).
        /// </summary>
        public float GetDamage(float chargePercent)
        {
            return Mathf.Lerp(minDamage, maxDamage, Mathf.Clamp01(chargePercent));
        }

        /// <summary>
        /// Calculates projectile speed based on charge percentage (0-1).
        /// </summary>
        public float GetProjectileSpeed(float chargePercent)
        {
            return Mathf.Lerp(minProjectileSpeed, maxProjectileSpeed, Mathf.Clamp01(chargePercent));
        }
    }
}
