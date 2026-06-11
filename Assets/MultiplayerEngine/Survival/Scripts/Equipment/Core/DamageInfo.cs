using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Types of damage that can be dealt.
    /// Used for damage resistance/weakness systems (expandable for future).
    /// </summary>
    public enum DamageType
    {
        Physical,   // Generic melee damage (sword)
        Chopping,   // Axe damage - effective vs trees
        Mining,     // Pickaxe damage - effective vs rocks
        Piercing,   // Arrow/bullet damage
        Fire,       // Future expansion
        Frost       // Future expansion
    }

    /// <summary>
    /// Contains all information about a damage event.
    /// Used for both combat and harvesting damage.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public ulong attackerId;      // NetworkObject ID of attacker
        public int weaponTier;        // For tier-locked resources

        public DamageInfo(float amount, DamageType type, Vector3 hitPoint, ulong attackerId, int tier = 1)
        {
            this.amount = amount;
            this.type = type;
            this.hitPoint = hitPoint;
            this.hitDirection = Vector3.zero;
            this.attackerId = attackerId;
            this.weaponTier = tier;
        }

        public DamageInfo WithDirection(Vector3 direction)
        {
            hitDirection = direction;
            return this;
        }

        public static DamageInfo Empty => new DamageInfo(0, DamageType.Physical, Vector3.zero, 0);
    }
}