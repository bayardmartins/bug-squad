namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for harvestable resources (trees, rocks, bushes).
    /// Separate from IDamageable for clear distinction between combat and harvesting.
    /// </summary>
    public interface IHarvestable
    {
        /// <summary>
        /// Unique identifier for this resource instance.
        /// </summary>
        int ResourceId { get; }

        /// <summary>
        /// The type of resource this is (Tree, Rock, etc.)
        /// </summary>
        HarvestableType ResourceType { get; }

        /// <summary>
        /// Minimum tool tier required to harvest this resource.
        /// </summary>
        int RequiredTier { get; }

        /// <summary>
        /// Apply harvest damage to this resource. Routes through ResourceManager.
        /// </summary>
        /// <param name="damage">Amount of harvest damage</param>
        /// <param name="hitPoint">World position of impact</param>
        /// <param name="attackerId">Network ID of harvester</param>
        /// <returns>True if damage request was sent</returns>
        bool TakeHarvestDamage(float damage, UnityEngine.Vector3 hitPoint, ulong attackerId);

        /// <summary>
        /// Whether this resource still exists.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Current health percentage (0-1) for UI.
        /// </summary>
        float HealthPercentage { get; }
    }
}