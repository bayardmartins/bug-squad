namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for any object that can receive damage.
    /// Implement on players, enemies, destructible objects.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Apply damage to this object. Called on server.
        /// </summary>
        /// <param name="damage">Damage information including amount, type, and source</param>
        /// <returns>True if damage was applied (hit was successful)</returns>
        bool TakeDamage(DamageInfo damage);

        /// <summary>
        /// Whether this target is still alive/active.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Current health percentage (0-1) for UI purposes.
        /// </summary>
        float HealthPercentage { get; }
    }
}