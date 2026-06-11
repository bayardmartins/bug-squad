using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Premium, context-sensitive building HUD overlay.
    /// Shows building hotkeys, action names, and dynamic resource cost tracking.
    /// Fades in/out smoothly depending on player state and equip status.
    /// </summary>
    public class BuildInfoUI : MonoBehaviour
    {
        [System.Serializable]
        public struct ResourceSlot
        {
            public GameObject root;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text countText;
        }

        [Header("UI Canvas Group")]
        [Tooltip("CanvasGroup used for smooth fading animations.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Main Panels")]
        [SerializeField] private GameObject resourcesPanel;

        [Header("Text Fields")]
        [SerializeField] private TMP_Text actionTitleText;
        [SerializeField] private TMP_Text hotkeysText;

        [Header("Resource Slots (Up to 3)")]
        [Tooltip("Pre-allocated slots for resource costs to avoid dynamic allocation overhead.")]
        [SerializeField] private ResourceSlot[] resourceSlots;

        [Header("Settings")]
        [Tooltip("Fade speed multiplier for opening and closing.")]
        [SerializeField] private float fadeSpeed = 8f;

        private BuildManager buildManager;
        private IInventoryManager inventoryManager;
        private float targetAlpha = 0f;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            buildManager = BuildManager.Instance;
            inventoryManager = LocalPlayerInstance.InventoryManager;
        }

        private void Update()
        {
            // Fallback find if missing
            if (buildManager == null)
                buildManager = BuildManager.Instance;
            
            inventoryManager = LocalPlayerInstance.InventoryManager;

            if (buildManager == null)
            {
                HideUI();
                return;
            }

            UpdateUIContext();
            HandleFadeAnimation();
        }

        /// <summary>
        /// Analyzes BuildManager state and updates UI content accordingly.
        /// </summary>
        private void UpdateUIContext()
        {
            // Case 1: Build Mode is Active (Ghost placement preview)
            if (buildManager.IsBuildModeActive && buildManager.CurrentGhost != null && buildManager.CurrentPiece != null)
            {
                ShowPlacementUI(buildManager.CurrentPiece);
            }
            // Case 2: Hammer equipped, looking at a blueprint structure (blueprint construction)
            else if (buildManager.IsHammerEquipped() && buildManager.HoveredPiece != null)
            {
                var hovered = buildManager.HoveredPiece;
                var entry = hovered.GetBuildPieceEntry();

                if (entry != null)
                {
                    if (hovered.isBlueprintPiece)
                    {
                        ShowBlueprintUI(entry);
                    }
                    else
                    {
                        ShowSolidDemolishUI(entry);
                    }
                }
                else
                {
                    HideUI();
                }
            }
            // Case 3: Hammer equipped but not looking at anything or not in build mode
            else
            {
                HideUI();
            }
        }

        /// <summary>
        /// Formats and shows the ghost piece placement UI.
        /// </summary>
        private void ShowPlacementUI(BuildPieceEntry piece)
        {
            targetAlpha = 1f;

            bool isBlueprintMode = buildManager.PlacementMode == BuildPlacementMode.Blueprint;
            string prefix = isBlueprintMode ? "<color=#52B2BF>BLUEPRINT:</color> " : "<color=#4CAF50>BUILDING:</color> ";
            
            if (actionTitleText != null)
                actionTitleText.text = $"{prefix}{piece.pieceName.ToUpper()}";

            // Set hotkeys text with stylish premium colors
            if (hotkeysText != null)
            {
                hotkeysText.text = "<color=#EAEAEA><b>[Q] / [E]</b></color> Rotate    •    <color=#EAEAEA><b>[LMB]</b></color> Place Preview    •    <color=#FF5252><b>[Esc]</b></color> Cancel";
            }

            // Display resource costs
            UpdateResourceCosts(piece.costs, isBlueprintMode ? "Required to Complete:" : "Placement Cost:");
        }

        /// <summary>
        /// Formats and shows the blueprint construction UI when looking at placed blueprint.
        /// </summary>
        private void ShowBlueprintUI(BuildPieceEntry piece)
        {
            targetAlpha = 1f;

            if (actionTitleText != null)
                actionTitleText.text = $"<color=#52B2BF>UPGRADE BLUEPRINT:</color> {piece.pieceName.ToUpper()}";

            if (hotkeysText != null)
            {
                hotkeysText.text = "<color=#4CAF50><b>[E]</b></color> Add Materials & Complete    •    <color=#FF5252><b>[X]</b></color> Demolish";
            }

            // Display resource costs
            UpdateResourceCosts(piece.costs, "Required Materials:");
        }

        /// <summary>
        /// Formats and shows structure info when looking at solid structure.
        /// </summary>
        private void ShowSolidDemolishUI(BuildPieceEntry piece)
        {
            targetAlpha = 1f;

            if (actionTitleText != null)
                actionTitleText.text = $"<color=#E57373>STRUCTURE:</color> {piece.pieceName.ToUpper()}";

            if (hotkeysText != null)
            {
                hotkeysText.text = "<color=#FF5252><b>[X]</b></color> Demolish / Remove";
            }

            // No resource requirements to show for demolish
            if (resourcesPanel != null)
                resourcesPanel.SetActive(false);

            DisableAllResourceSlots();
        }

        /// <summary>
        /// Updates the resource display slots with required items.
        /// </summary>
        private void UpdateResourceCosts(ResourceCost[] costs, string sectionTitle)
        {
            if (costs == null || costs.Length == 0)
            {
                if (resourcesPanel != null)
                    resourcesPanel.SetActive(false);
                DisableAllResourceSlots();
                return;
            }

            if (resourcesPanel != null)
                resourcesPanel.SetActive(true);

            for (int i = 0; i < resourceSlots.Length; i++)
            {
                var slot = resourceSlots[i];
                if (slot.root == null) continue;

                if (i < costs.Length && costs[i]?.resource != null)
                {
                    var cost = costs[i];
                    var item = cost.resource;

                    slot.root.SetActive(true);

                    if (slot.icon != null)
                    {
                        slot.icon.sprite = item.itemIcon;
                        slot.icon.gameObject.SetActive(item.itemIcon != null);
                    }

                    if (slot.nameText != null)
                    {
                        slot.nameText.text = item.itemName;
                    }

                    if (slot.countText != null)
                    {
                        int haveCount = 0;
                        if (inventoryManager != null)
                        {
                            haveCount = inventoryManager.GetTotalCount(item.itemId);
                        }

                        // Apply dynamic color coding: soft red if not enough, white/green if enough
                        if (haveCount >= cost.amount)
                        {
                            slot.countText.text = $"<color=#A5D6A7>{haveCount}</color>/{cost.amount}";
                        }
                        else
                        {
                            slot.countText.text = $"<color=#EF9A9A>{haveCount}</color>/{cost.amount}";
                        }
                    }
                }
                else
                {
                    slot.root.SetActive(false);
                }
            }
        }

        private void DisableAllResourceSlots()
        {
            for (int i = 0; i < resourceSlots.Length; i++)
            {
                if (resourceSlots[i].root != null)
                    resourceSlots[i].root.SetActive(false);
            }
        }

        private void HideUI()
        {
            targetAlpha = 0f;
        }

        private void HandleFadeAnimation()
        {
            if (canvasGroup == null) return;

            // Smooth linear interpolation for modern, fluid fade feel
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            
            // Enable/disable raycast blocking depending on visibility
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.05f;
            canvasGroup.interactable = canvasGroup.alpha > 0.05f;
        }
    }
}
