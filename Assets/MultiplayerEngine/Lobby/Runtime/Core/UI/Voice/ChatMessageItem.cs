using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents a single chat message item in the chat UI.
    /// Requires a VerticalLayoutGroup on the parent container for proper stacking.
    /// </summary>
    public class ChatMessageItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text senderNameText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image backgroundImage;
        
        [Header("Background Colors")]
        [SerializeField] private Color localPlayerBgColor = new Color(0.15f, 0.35f, 0.55f, 0.9f);
        [SerializeField] private Color otherPlayerBgColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        [SerializeField] private Color systemMessageBgColor = new Color(0.4f, 0.35f, 0.15f, 0.9f);

        [Header("Name Text Colors")]
        [SerializeField] private Color localPlayerNameColor = new Color(0.4f, 0.8f, 1f);     // Cyan-ish
        [SerializeField] private Color otherPlayerNameColor = new Color(1f, 0.85f, 0.4f);    // Gold-ish
        [SerializeField] private Color systemNameColor = new Color(1f, 0.6f, 0.2f);          // Orange

        [Header("Message Text Colors")]
        [SerializeField] private Color localPlayerTextColor = Color.white;
        [SerializeField] private Color otherPlayerTextColor = new Color(0.9f, 0.9f, 0.9f);   // Light gray
        [SerializeField] private Color systemTextColor = new Color(1f, 0.9f, 0.6f);          // Light yellow

        /// <summary>
        /// Sets the message content and styling.
        /// </summary>
        /// <param name="senderName">Name of the sender.</param>
        /// <param name="message">The message content.</param>
        /// <param name="isLocalPlayer">Whether this is from the local player.</param>
        public void SetMessage(string senderName, string message, bool isLocalPlayer)
        {
            bool isSystem = senderName == "System";

            // Set sender name
            if (senderNameText != null)
            {
                senderNameText.text = senderName + ":";
                
                if (isSystem)
                    senderNameText.color = systemNameColor;
                else if (isLocalPlayer)
                    senderNameText.color = localPlayerNameColor;
                else
                    senderNameText.color = otherPlayerNameColor;
            }

            // Set message text
            if (messageText != null)
            {
                messageText.text = message;
                
                if (isSystem)
                    messageText.color = systemTextColor;
                else if (isLocalPlayer)
                    messageText.color = localPlayerTextColor;
                else
                    messageText.color = otherPlayerTextColor;
            }

            // Set background color
            if (backgroundImage != null)
            {
                if (isSystem)
                    backgroundImage.color = systemMessageBgColor;
                else if (isLocalPlayer)
                    backgroundImage.color = localPlayerBgColor;
                else
                    backgroundImage.color = otherPlayerBgColor;
            }
        }
    }
}
