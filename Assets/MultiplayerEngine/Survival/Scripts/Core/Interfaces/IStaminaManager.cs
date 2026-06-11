using System;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for managing player stamina.
    /// Used to decouple stamina logic from core controllers like EquipmentController.
    /// </summary>
    public interface IStaminaManager
    {
        /// <summary>
        /// Maximum amount of stamina the player can have.
        /// </summary>
        float MaxStamina { get; }

        /// <summary>
        /// Current amount of stamina the player has.
        /// </summary>
        float CurrentStamina { get; }

        /// <summary>
        /// Current stamina represented as a percentage (0.0 to 1.0).
        /// </summary>
        float StaminaPercentage { get; }

        /// <summary>
        /// Whether the player has enough stamina to perform a basic action.
        /// </summary>
        bool CanAct { get; }

        /// <summary>
        /// Event fired when stamina changes. Provides current and max stamina.
        /// </summary>
        event Action<float, float> OnStaminaChanged;

        /// <summary>
        /// Attempts to use stamina for an action.
        /// Returns true if successful (enough stamina available).
        /// </summary>
        bool TryUseStamina(float amount);

        /// <summary>
        /// Quickly checks if stamina is available without consuming.
        /// </summary>
        bool HasStamina(float amount);

        /// <summary>
        /// Instantly restores stamina (e.g., from consumable).
        /// </summary>
        void RestoreStamina(float amount);

        /// <summary>
        /// Sets max stamina (for upgrades/buffs).
        /// </summary>
        void SetMaxStamina(float newMax);
    }
}