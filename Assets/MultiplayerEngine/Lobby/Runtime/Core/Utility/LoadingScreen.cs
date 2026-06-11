using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handles the loading screen display during game transitions.
    /// Shows progress and status during connection, scene loading, and player synchronization.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Slider progressBar;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.3f;

        private float targetAlpha;
        private bool isFading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Start hidden
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (progressBar != null)
                progressBar.value = 0f;
        }

        private void Update()
        {
            if (!isFading || canvasGroup == null) return;

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime / fadeDuration);

            if (Mathf.Approximately(canvasGroup.alpha, targetAlpha))
            {
                isFading = false;
                canvasGroup.interactable = targetAlpha > 0.5f;
                canvasGroup.blocksRaycasts = targetAlpha > 0.5f;
            }
        }

        /// <summary>
        /// Shows the loading screen with optional initial status.
        /// </summary>
        public void ShowLoading(string status = "Loading...")
        {
            if (canvasGroup != null)
            {
                targetAlpha = 1f;
                isFading = true;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            UpdateStatus(status);
            SetProgress(0f);
        }

        /// <summary>
        /// Hides the loading screen.
        /// </summary>
        public void HideLoading()
        {
            if (canvasGroup != null)
            {
                targetAlpha = 0f;
                isFading = true;
            }
        }

        /// <summary>
        /// Updates the status text displayed on the loading screen.
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }

        /// <summary>
        /// Sets the progress bar value (0-1).
        /// </summary>
        public void SetProgress(float progress)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(progress);
        }
    }
}
