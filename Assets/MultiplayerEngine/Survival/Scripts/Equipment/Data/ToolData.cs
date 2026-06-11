using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Types of harvestable resources.
    /// </summary>
    public enum HarvestableType
    {
        Tree,
        Rock,
        Bush,
        Ore,
        Terrain
    }

    /// <summary>
    /// ScriptableObject containing tool-specific data.
    /// Tools harvest resources using BladeHitbox for hit detection.
    /// </summary>
    public class ToolData : ScriptableObject
    {
        [Header("Tool Stats")]
        [Tooltip("Tool tier (higher tier = harvest higher tier resources)")]
        public int tier = 1;

        [Tooltip("Damage dealt to harvestable objects per hit")]
        public float harvestDamage = 10f;

        [Tooltip("Types of resources this tool can harvest")]
        public HarvestableType[] canHarvest = { HarvestableType.Tree };

        [Header("Usage")]
        [Tooltip("Cooldown between uses in seconds")]
        public float useCooldown = 0.5f;

        [Tooltip("Stamina cost per swing")]
        public float staminaCost = 8f;

        /// <summary>
        /// Checks if this tool can harvest the specified type.
        /// </summary>
        public bool CanHarvest(HarvestableType type)
        {
            if (canHarvest == null || canHarvest.Length == 0)
                return true;

            foreach (var t in canHarvest)
            {
                if (t == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if tool tier meets requirement.
        /// </summary>
        public bool MeetsTierRequirement(int requiredTier)
        {
            return tier >= requiredTier;
        }
    }
}
