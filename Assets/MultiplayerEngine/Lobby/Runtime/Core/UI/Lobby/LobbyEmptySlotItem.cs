using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Visual placeholder item for empty player slots in the lobby.
    /// Displays an invite icon to indicate available spots.
    /// </summary>
    public class LobbyEmptySlotItem : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Image displaying the invite icon.")]
        [SerializeField] private Image inviteImage;

        [Tooltip("Optional background image for styling.")]
        [SerializeField] private Image backgroundImage;

        /// <summary>
        /// Sets the invite icon sprite.
        /// </summary>
        /// <param name="sprite">Sprite to display as the invite icon.</param>
        public void SetInviteIcon(Sprite sprite)
        {
            if (inviteImage != null && sprite != null)
            {
                inviteImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Sets the background color of the invite item.
        /// </summary>
        /// <param name="color">Color for the background.</param>
        public void SetBackgroundColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }
    }
}
