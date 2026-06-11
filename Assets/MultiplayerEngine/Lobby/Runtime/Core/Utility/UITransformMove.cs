using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Slides a UI panel in/out with eased animation.
    /// </summary>
    public class UITransformMove : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] private RectTransform uiElement;
        [SerializeField] private Vector2 targetPosition;
        [SerializeField] private float duration = 0.2f;

        [Header("Show Button")]
        [SerializeField] private Button showButton;

        [Header("Click Outside Settings")]
        [Tooltip("RectTransforms that should NOT trigger hide when clicked")]
        [SerializeField] private List<RectTransform> ignoreClickAreas = new();

        private Vector2 startPosition;
        private Vector2 currentTargetPosition;
        private bool isOpen;
        private bool isAnimating;

        public bool IsOpen => isOpen;

        /// <summary>
        /// Called when panel starts hiding.
        /// </summary>
        public event System.Action OnHide;

        private void Start()
        {
            startPosition = uiElement.anchoredPosition;
            currentTargetPosition = startPosition;

            showButton?.onClick.AddListener(() => _ = ShowAsync());
        }

        /// <summary>
        /// Shows the panel with animation.
        /// </summary>
        public async Task ShowAsync()
        {
            if (isOpen || isAnimating) return;
            isAnimating = true;
            currentTargetPosition = targetPosition;

            // Hide show button when panel opens
            if (showButton != null)
                showButton.gameObject.SetActive(false);

            await MoveUIAsync(targetPosition);
            isOpen = true;
            isAnimating = false;
        }

        /// <summary>
        /// Hides the panel with animation.
        /// </summary>
        public async Task HideAsync()
        {
            if (!isOpen || isAnimating) return;
            isAnimating = true;
            currentTargetPosition = startPosition;

            // Fire OnHide event before hiding
            OnHide?.Invoke();

            await MoveUIAsync(startPosition);
            isOpen = false;
            isAnimating = false;

            // Show the show button when panel closes
            if (showButton != null)
                showButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Toggles the panel visibility.
        /// </summary>
        public async Task<bool> ToggleMoveAsync()
        {
            if (isOpen)
                await HideAsync();
            else
                await ShowAsync();
            return isOpen;
        }

        private async Task MoveUIAsync(Vector2 target)
        {
            Vector2 currentPos = uiElement.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float t = elapsedTime / duration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                uiElement.anchoredPosition = Vector2.Lerp(currentPos, target, eased);
                elapsedTime += Time.deltaTime;
                await Task.Yield();

                if (target != currentTargetPosition)
                    return;
            }

            uiElement.anchoredPosition = target;
        }

        private void Update()
        {
            if (!isOpen || isAnimating) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // Check if click is on the main panel
            if (RectTransformUtility.RectangleContainsScreenPoint(uiElement, Input.mousePosition, null))
                return;

            // Check if click is on any ignored areas (like popups)
            foreach (var area in ignoreClickAreas)
            {
                if (area != null && RectTransformUtility.RectangleContainsScreenPoint(area, Input.mousePosition, null))
                    return;
            }

            // Click was outside - hide the panel
            _ = HideAsync();
        }

        /// <summary>
        /// Adds a RectTransform to ignore list at runtime (e.g., spawned popups).
        /// </summary>
        public void AddIgnoreArea(RectTransform area)
        {
            if (area != null && !ignoreClickAreas.Contains(area))
                ignoreClickAreas.Add(area);
        }

        /// <summary>
        /// Removes a RectTransform from ignore list.
        /// </summary>
        public void RemoveIgnoreArea(RectTransform area)
        {
            ignoreClickAreas.Remove(area);
        }
    }
}

