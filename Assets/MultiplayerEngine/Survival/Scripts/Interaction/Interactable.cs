using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Type of interaction for HUD display.
    /// - Pickup: Shows key icon, "Pickup" action, name, description
    /// - Interact: Shows key icon, "Interact" action, name, description  
    /// - Details: Shows only name and description (no key icon)
    /// </summary>
    public enum InteractionType
    {
        Pickup,
        Interact,
        Details
    }

    /// <summary>
    /// Base class for all interactable objects in the world.
    /// Objects can be interacted with by looking at them and pressing the interact key.
    /// </summary>
    public class Interactable : NetworkBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Name shown in the interaction HUD. If empty, uses GameObject name.")]
        [SerializeField] protected string displayName;

        [Tooltip("Description shown in the HUD.")]
        [SerializeField] protected string description;

        [Tooltip("Type of interaction (Pickup, Interact, Details).")]
        [SerializeField] protected InteractionType interactionType = InteractionType.Interact;

        [Tooltip("Icon shown in the interaction HUD.")]
        [SerializeField] protected Sprite interactionIcon;

        /// <summary>
        /// The name to display in the interaction HUD.
        /// </summary>
        public virtual string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        /// <summary>
        /// The type of interaction.
        /// </summary>
        public virtual InteractionType InteractionType => interactionType;

        /// <summary>
        /// Icon shown in the interaction HUD.
        /// </summary>
        public virtual Sprite GetIcon() => interactionIcon;

        /// <summary>
        /// Description text to show in the HUD.
        /// </summary>
        public virtual string GetDescription() => description;

        /// <summary>
        /// Additional info to show (e.g., item count "x5").
        /// Override in derived classes.
        /// </summary>
        public virtual string GetInteractionInfo() => null;

        /// <summary>
        /// Called when the player interacts with this object.
        /// Override in derived classes to implement custom behavior.
        /// </summary>
        public virtual void Interact()
        {
            // Override in derived classes
        }
    }
}