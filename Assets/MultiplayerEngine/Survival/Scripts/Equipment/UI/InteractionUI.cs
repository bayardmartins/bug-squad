using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Centralized UI for interaction prompts.
    /// Shows: [Key Icon] [Action] [Name] [Description]
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        public static InteractionUI Instance { get; private set; }

        [Header("UI Elements")]
        [Tooltip("Icon showing the interaction key (keyboard or gamepad)")]
        [SerializeField] private Image keyIcon;

        [Tooltip("Optional icon showing the item's sprite (for pickable items)")]
        [SerializeField] private Image itemIcon;

        [Tooltip("Text showing the action (e.g., 'Pickup', 'Interact')")]
        [SerializeField] private TMP_Text actionText;

        [Tooltip("Text showing the object name")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("Text showing the description")]
        [SerializeField] private TMP_Text descriptionText;

        [Header("Key Icons")]
        [SerializeField] private Sprite keyboardIcon;
        [SerializeField] private Sprite gamepadIcon;

        [Header("Animation")]
        [Tooltip("Time in seconds to fade in/out (0 = instant)")]
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private CanvasGroup canvasGroup;

        private bool isVisible;
        private float currentAlpha;
        private float targetAlpha;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            currentAlpha = 0f;
            targetAlpha = 0f;
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (canvasGroup == null) return;

            if (fadeDuration <= 0f)
                currentAlpha = targetAlpha;
            else
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);

            canvasGroup.alpha = currentAlpha;
            canvasGroup.blocksRaycasts = currentAlpha > 0.01f;
        }

        /// <summary>
        /// Show interaction prompt.
        /// </summary>
        public void Show(InteractionType type, string objectName, string description = null, string info = null, Sprite icon = null)
        {
            isVisible = true;
            targetAlpha = 1f;

            // Key icon - show keyboard or gamepad
            if (keyIcon != null)
            {
                bool isGamepad = IsGamepadConnected();
                keyIcon.sprite = isGamepad ? gamepadIcon : keyboardIcon;
                // Hide key icon for Details type
                keyIcon.gameObject.SetActive(type != InteractionType.Details && keyIcon.sprite != null);
            }

            // Item icon - show if available
            if (itemIcon != null)
            {
                bool hasIcon = icon != null;
                itemIcon.gameObject.SetActive(hasIcon);
                if (hasIcon)
                    itemIcon.sprite = icon;
            }

            // Action text - "Pickup", "Interact", or hide for Details
            if (actionText != null)
            {
                actionText.gameObject.SetActive(type != InteractionType.Details);
                actionText.text = type.ToString();
            }

            // Name - combine with info if present (e.g., "Wood Log x5")
            if (nameText != null)
            {
                string displayName = objectName ?? "";
                if (!string.IsNullOrEmpty(info))
                    displayName += $" {info}";
                nameText.text = displayName;
            }

            // Description
            if (descriptionText != null)
            {
                bool hasDesc = !string.IsNullOrEmpty(description);
                descriptionText.gameObject.SetActive(hasDesc);
                descriptionText.text = description ?? "";
            }
        }

        /// <summary>
        /// Hide the UI.
        /// </summary>
        public void Hide()
        {
            isVisible = false;
            targetAlpha = 0f;
        }

        private bool IsGamepadConnected()
        {
            return Gamepad.current != null;
        }

        public bool IsVisible => isVisible;
    }
}