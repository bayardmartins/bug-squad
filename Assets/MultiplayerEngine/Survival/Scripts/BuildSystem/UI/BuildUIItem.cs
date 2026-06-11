using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Single category slot in the build menu.
    /// Shows current piece with arrows to cycle through pieces in this category.
    /// Now uses BuildDatabase instead of separate BuildCategoryData assets.
    /// </summary>
    public class BuildUIItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("Category")]
        [Tooltip("Which category this slot shows (Floor, Wall, Roof, etc.)")]
        [SerializeField] private BuildCategory category;

        // Runtime - populated from BuildDatabase
        private BuildMenuUI menuUI;
        private int slotIndex;
        private int currentPieceIndex;
        private bool isSelected;
        private bool isHovered;
        private Vector3 originalScale;
        private BuildPieceEntry[] pieces; // Pieces for this category

        // Properties
        public BuildPieceEntry CurrentPiece => 
            pieces != null && pieces.Length > 0 && currentPieceIndex < pieces.Length
                ? pieces[currentPieceIndex]
                : null;

        public int PieceCount => pieces?.Length ?? 0;
        public BuildCategory Category => category;

        private void Awake()
        {
            CaptureOriginalScale();

            // Setup button listeners
            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(PreviousPiece);
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(NextPiece);
            }
        }

        /// <summary>
        /// Captures the original scale. Called from both Awake and Initialize
        /// to handle script execution order issues.
        /// </summary>
        private void CaptureOriginalScale()
        {
            // Only capture if not already captured with a valid value
            if (originalScale != Vector3.zero && originalScale.magnitude >= 0.01f)
                return;

            originalScale = transform.localScale;
            
            // If still zero, use Vector3.one as fallback
            if (originalScale == Vector3.zero || originalScale.magnitude < 0.01f)
            {
                originalScale = Vector3.one;
            }
        }

        public void Initialize(BuildMenuUI menu, int index, BuildDatabase database)
        {
            // Ensure scale is captured (in case Awake hasn't run yet)
            CaptureOriginalScale();
            
            menuUI = menu;
            slotIndex = index;
            currentPieceIndex = 0;

            // Load pieces for this category from database
            if (database != null)
            {
                var pieceList = database.GetPiecesByCategory(category);
                pieces = pieceList.ToArray();
            }
            else
            {
                pieces = new BuildPieceEntry[0];
                Debug.LogWarning($"BuildUIItem: No BuildDatabase assigned for category {category}");
            }

            UpdateVisuals();
        }

        #region Piece Cycling

        public void NextPiece()
        {
            if (PieceCount <= 1) return;

            currentPieceIndex = (currentPieceIndex + 1) % PieceCount;
            UpdateVisuals();
            menuUI?.NotifyPieceChanged(slotIndex, CurrentPiece);
        }

        public void PreviousPiece()
        {
            if (PieceCount <= 1) return;

            currentPieceIndex = (currentPieceIndex - 1 + PieceCount) % PieceCount;
            UpdateVisuals();
            menuUI?.NotifyPieceChanged(slotIndex, CurrentPiece);
        }

        #endregion

        #region State

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisuals();
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            UpdateVisuals();
        }

        #endregion

        #region Visuals

        private void UpdateVisuals()
        {
            var piece = CurrentPiece;

            // Icon
            if (iconImage != null)
            {
                Sprite icon = piece?.icon;
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.color = Color.clear;
                }
            }

            // Title - show piece name or category name
            if (titleText != null)
            {
                titleText.text = piece?.pieceName ?? category.ToString();
            }

            // Background color based on state
            if (backgroundImage != null && menuUI != null)
            {
                if (isSelected)
                    backgroundImage.color = menuUI.selectedColor;
                else if (isHovered)
                    backgroundImage.color = menuUI.hoverColor;
                else
                    backgroundImage.color = menuUI.normalColor;
            }

            // Scale for hover/selected effect (1.2x when active)
            float hoverScaleValue = menuUI?.hoverScale ?? 1.2f;
            float scale = (isSelected || isHovered) ? hoverScaleValue : 1f;
            transform.localScale = originalScale * scale;

            // Show/hide arrows based on piece count
            bool hasMultiple = PieceCount > 1;
            if (prevButton != null) prevButton.gameObject.SetActive(hasMultiple);
            if (nextButton != null) nextButton.gameObject.SetActive(hasMultiple);
        }

        #endregion

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            menuUI?.OnSlotHover(slotIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            menuUI?.OnSlotUnhover(slotIndex);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                menuUI?.SelectSlot(slotIndex);
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (prevButton != null) prevButton.onClick.RemoveAllListeners();
            if (nextButton != null) nextButton.onClick.RemoveAllListeners();
        }
    }
}
