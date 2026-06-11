using System;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for unified player stats management: Health, Stamina, Food, Water.
    /// Used to decouple player stat logic from character and equipment controllers.
    /// </summary>
    public interface IPlayerStatsManager
    {
        // Add minimal properties required by controllers
        
        /// <summary>
        /// Gets the current PlayerStats struct securely.
        /// </summary>
        PlayerStatsManager.PlayerStats Stats { get; }
        
        /// <summary>
        /// Returns whether the player is currently alive (health > 0).
        /// </summary>
        bool IsAlive { get; }
        
        /// <summary>
        /// Current health percentage (0.0 to 1.0).
        /// </summary>
        float HealthPercentage { get; }
        
        /// <summary>
        /// Current stamina value.
        /// </summary>
        float CurrentStamina { get; }
        
        /// <summary>
        /// Maximum stamina value.
        /// </summary>
        float MaxStamina { get; }

        /// <summary>
        /// Checks if the player has enough stamina to perform basic actions.
        /// </summary>
        bool CanAct { get; }
        
        /// <summary>
        /// Checks if the player has enough stamina to run.
        /// </summary>
        bool CanRun { get; }

        /// <summary>
        /// Event fired when player stats change.
        /// </summary>
        event Action<PlayerStatsManager.PlayerStats> OnStatsChanged;
        
        /// <summary>
        /// Event fired when the player dies.
        /// </summary>
        event Action OnDeath;

        /// <summary>
        /// Tell the stats manager if the player is currently running (for stamina drain).
        /// </summary>
        void SetRunning(bool running);

        /// <summary>
        /// Attempts to use an amount of stamina (client-predicted).
        /// </summary>
        bool TryUseStamina(float amount);

        /// <summary>
        /// Quickly checks if stamina is available without consuming.
        /// </summary>
        bool HasStamina(float amount);
        
        /// <summary>
        /// Restores stats (typically from consumables or resting).
        /// </summary>
        void RestoreStatsRpc(float health, float stamina, float food, float water);

        /// <summary>
        /// Heals the player for a specific amount.
        /// </summary>
        void Heal(float amount);
    }
}