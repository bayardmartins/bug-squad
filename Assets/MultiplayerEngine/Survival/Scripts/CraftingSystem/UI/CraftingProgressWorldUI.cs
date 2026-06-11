using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// World-space progress bar shown on the crafting table during crafting.
    /// Visible to all players. Does not billboard to camera - maintains fixed rotation.
    /// Shows crafting progress with item icon, countdown timer, and fill image.
    /// Remains visible (green) after completion until item is equipped.
    /// Attach this script to the root UI panel that will be shown/hidden.
    /// </summary>
    public class CraftingProgressWorldUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text showing the recipe/item name")]
        [SerializeField] private TMPro.TMP_Text recipeNameText;
        
        [Tooltip("Fill image for the progress bar (set Image Type to Filled)")]
        [SerializeField] private Image fillImage;
        
        [Tooltip("Image showing the icon of the item being crafted")]
        [SerializeField] private Image itemIcon;
        
        [Tooltip("Text showing the countdown timer (remaining time)")]
        [SerializeField] private TMPro.TMP_Text countdownText;
        
        [Tooltip("Text showing percentage progress (e.g. 50%)")]
        [SerializeField] private TMPro.TMP_Text progressText;

        [Header("Visual Settings")]
        [Tooltip("Color of the progress bar during crafting")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.6f, 1f);
        
        [Tooltip("Color of the progress bar when crafting is complete (waiting for pickup)")]
        [SerializeField] private Color completeColor = new Color(0.2f, 1f, 0.3f);

        // State tracking
        private bool isEnabled = false;
        private bool isCraftingComplete = false;
        private float totalCraftingTime;
        private float currentProgress;

        private void Start()
        {
            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Enable or disable the world UI. When disabled (no item in slot), UI won't show.
        /// Call this when an item is assigned to the crafting slot.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            
            if (!enabled)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set the item icon to display during crafting.
        /// </summary>
        public void SetItemIcon(Sprite icon)
        {
            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.gameObject.SetActive(icon != null);
            }
        }

        /// <summary>
        /// Set the total crafting time for countdown calculation.
        /// </summary>
        public void SetCraftingTime(float time)
        {
            totalCraftingTime = time;
        }

        /// <summary>
        /// Update the progress display.
        /// </summary>
        /// <param name="progress">Progress value from 0 to 1</param>
        /// <param name="isCrafting">Whether crafting is currently in progress</param>
        public void UpdateProgress(float progress, bool isCrafting)
        {
            if (!isEnabled) return;
            
            currentProgress = progress;
            
            // Update fill image progress (use fillAmount for Image Type: Filled)
            if (fillImage != null)
            {
                fillImage.fillAmount = progress;
                fillImage.color = progress >= 1f ? completeColor : normalColor;
            }
            
            // Mark as complete - hide text elements
            if (progress >= 1f)
            {
                isCraftingComplete = true;
                
                // Hide percentage, time, and name when complete
                if (progressText != null)
                    progressText.gameObject.SetActive(false);
                if (countdownText != null)
                    countdownText.gameObject.SetActive(false);
                if (recipeNameText != null)
                    recipeNameText.gameObject.SetActive(false);
            }
            else
            {
                // Show and update texts during crafting
                if (progressText != null)
                {
                    progressText.gameObject.SetActive(true);
                    int percent = Mathf.RoundToInt(progress * 100f);
                    progressText.text = $"{percent}%";
                }

                if (countdownText != null && isCrafting && totalCraftingTime > 0)
                {
                    countdownText.gameObject.SetActive(true);
                    float remainingTime = totalCraftingTime * (1f - progress);
                    countdownText.text = FormatTime(remainingTime);
                }
                
                if (recipeNameText != null)
                    recipeNameText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Set crafting state and recipe name.
        /// </summary>
        /// <param name="isCrafting">Whether crafting is in progress</param>
        /// <param name="recipeName">Name of the recipe being crafted</param>
        public void SetCrafting(bool isCrafting, string recipeName)
        {
            if (!isEnabled) return;
            
            // Show panel when crafting starts
            // Keep visible after completion until Hide() is called
            if (isCrafting)
            {
                gameObject.SetActive(true);
                isCraftingComplete = false;
            }
            else if (!isCraftingComplete)
            {
                // Only hide if not completed (was cancelled or not started)
                gameObject.SetActive(false);
            }
            // If complete, keep showing until Hide() is explicitly called

            if (recipeNameText != null)
            {
                recipeNameText.text = recipeName;
            }

            if (fillImage != null && !isCrafting && !isCraftingComplete)
            {
                fillImage.fillAmount = 0f;
            }
            
            if (progressText != null && !isCrafting && !isCraftingComplete)
            {
                progressText.text = "0%";
            }
            
            if (countdownText != null && !isCrafting && !isCraftingComplete)
            {
                countdownText.text = "";
            }
        }

        /// <summary>
        /// Call when the crafted item has been equipped/picked up by any player.
        /// Hides the progress UI until a new crafting session starts.
        /// </summary>
        public void Hide()
        {
            isCraftingComplete = false;
            currentProgress = 0f;
            
            gameObject.SetActive(false);
            
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                fillImage.color = normalColor;
            }
            
            if (progressText != null)
            {
                progressText.text = "";
            }
            
            if (countdownText != null)
            {
                countdownText.text = "";
            }
            
            if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Check if crafting is complete and waiting for pickup.
        /// </summary>
        public bool IsComplete => isCraftingComplete;

        /// <summary>
        /// Format time as MM:SS or just seconds if under a minute.
        /// </summary>
        private string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0s";
            
            if (seconds < 60)
            {
                return $"{Mathf.CeilToInt(seconds)}s";
            }
            else
            {
                int mins = Mathf.FloorToInt(seconds / 60);
                int secs = Mathf.CeilToInt(seconds % 60);
                return $"{mins}:{secs:D2}";
            }
        }
    }
}