using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    public enum MarkerType
    {
        Player,
        Item,
        Objective,
        Enemy
    }

    /// <summary>
    /// Individual marker UI element — one per tracked target.
    /// Handles on-screen (icon/name/distance) and off-screen (icon + rotating arrow).
    /// Driven each frame by <see cref="MarkerManager"/>.
    /// </summary>
    public class MarkerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private Image arrowImage;
        [SerializeField] private Slider HealthBar;

        [Header("Off-Screen Arrow")]
        [Tooltip("Distance from the arrow pivot to the icon center (pixels)")]
        [SerializeField] private float arrowOffset = 40f;

        private RectTransform rectTransform;
        private RectTransform arrowRect;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (arrowImage != null)
                arrowRect = arrowImage.GetComponent<RectTransform>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Main update called by MarkerManager every frame.
        /// </summary>
        public void UpdateMarker(MarkerTarget target, Camera cam, RectTransform canvasRect,
                                  Camera canvasCamera,
                                  float closeRange, float farRange, float edgePadding)
        {
            if (target == null || cam == null) return;

            Vector3 worldPos = target.MarkerWorldPosition;
            Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);

            // z < 0 means the target is behind the camera
            bool isBehind = viewportPos.z < 0f;
            bool isOnScreen = !isBehind
                              && viewportPos.x >= 0f && viewportPos.x <= 1f
                              && viewportPos.y >= 0f && viewportPos.y <= 1f;

            float distance = Vector3.Distance(cam.transform.position, worldPos);

            // Apply icon and color
            if (iconImage != null)
            {
                iconImage.sprite = target.Icon;
                iconImage.color = target.MarkerColor;
                iconImage.enabled = target.Icon != null;
            }

            if (isOnScreen)
            {
                HandleOnScreen(target, cam, canvasCamera, worldPos, distance, canvasRect, closeRange, farRange);
            }
            else
            {
                HandleOffScreen(target, cam, worldPos, canvasRect, edgePadding, isBehind);
            }
        }

        // ───────────────────────── On-Screen ─────────────────────────

        private void HandleOnScreen(MarkerTarget target, Camera cam, Camera canvasCamera,
                                     Vector3 worldPos, float distance, RectTransform canvasRect,
                                     float closeRange, float farRange)
        {
            // Hide arrow
            SetArrowVisible(false);

            // Convert world → screen → canvas local position
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvasCamera, out localPoint);
            rectTransform.localPosition = localPoint;

            // Decide what to show based on distance
            if (distance <= closeRange)
            {
                // Close: Icon + Name
                SetNameVisible(true);
                SetDistanceVisible(false);

                if (nameText != null)
                {
                    nameText.text = target.DisplayName;
                    nameText.color = target.MarkerColor;
                }

                // Show health bar if target provides health data
                SetHealthBarVisible(target.HealthPercent >= 0f);
                if (HealthBar != null && target.HealthPercent >= 0f)
                    HealthBar.value = target.HealthPercent;
            }
            else if (distance <= farRange)
            {
                // Medium: Icon + Distance
                SetNameVisible(false);
                SetDistanceVisible(true);

                if (distanceText != null)
                {
                    distanceText.text = FormatDistance(distance);
                    distanceText.color = target.MarkerColor;
                }
            }
            else
            {
                // Very far: Icon only
                SetNameVisible(false);
                SetDistanceVisible(false);
                SetHealthBarVisible(false);
            }
        }

        // ───────────────────────── Off-Screen ─────────────────────────

        private void HandleOffScreen(MarkerTarget target, Camera cam, Vector3 worldPos,
                                      RectTransform canvasRect, float edgePadding, bool isBehind)
        {
            // Off-screen: hide text and health bar, show arrow
            SetNameVisible(false);
            SetDistanceVisible(false);
            SetHealthBarVisible(false);
            SetArrowVisible(true);

            // Get direction from screen center to the target in screen space
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // If behind the camera, flip to keep the arrow pointing correctly
            if (isBehind)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)screenPos - screenCenter).normalized;

            // Clamp to screen edges with padding
            Vector2 canvasSize = canvasRect.sizeDelta;
            float halfW = canvasSize.x * 0.5f - edgePadding;
            float halfH = canvasSize.y * 0.5f - edgePadding;

            // Find the point where the ray from center hits the padded rect edge
            Vector2 clampedPos = Vector2.zero;
            if (Mathf.Abs(dir.x) > 0.001f && Mathf.Abs(dir.y) > 0.001f)
            {
                float tX = halfW / Mathf.Abs(dir.x);
                float tY = halfH / Mathf.Abs(dir.y);
                float t = Mathf.Min(tX, tY);
                clampedPos = dir * t;
            }
            else if (Mathf.Abs(dir.x) > 0.001f)
            {
                clampedPos = dir * (halfW / Mathf.Abs(dir.x));
                clampedPos.y = Mathf.Clamp(clampedPos.y, -halfH, halfH);
            }
            else
            {
                clampedPos = dir * (halfH / Mathf.Abs(dir.y));
                clampedPos.x = Mathf.Clamp(clampedPos.x, -halfW, halfW);
            }

            rectTransform.localPosition = clampedPos;

            // Rotate arrow to point toward target
            if (arrowRect != null)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                // Position arrow slightly outward from icon center
                arrowRect.localPosition = dir * arrowOffset;
            }
        }

        // ───────────────────────── Helpers ─────────────────────────

        private void SetNameVisible(bool visible)
        {
            if (nameText != null)
                nameText.gameObject.SetActive(visible);
        }

        private void SetDistanceVisible(bool visible)
        {
            if (distanceText != null)
                distanceText.gameObject.SetActive(visible);
        }

        private void SetArrowVisible(bool visible)
        {
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(visible);
        }

        private void SetHealthBarVisible(bool visible)
        {
            if (HealthBar != null)
                HealthBar.gameObject.SetActive(visible);
        }

        private string FormatDistance(float distance)
        {
            if (distance >= 1000f)
                return $"{distance / 1000f:F1}km";
            return $"{Mathf.RoundToInt(distance)}m";
        }

        /// <summary>
        /// Reset visuals before returning to pool.
        /// </summary>
        public void ResetMarker()
        {
            SetNameVisible(false);
            SetDistanceVisible(false);
            SetArrowVisible(false);
            SetHealthBarVisible(false);
            gameObject.SetActive(false);
        }
    }
}