using UnityEngine;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Thin UI layer for authentication. Subscribes to AuthenticationManager events
    /// and shows/hides panels accordingly. All flow logic is in AuthenticationManager.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AuthenticationUI : MonoBehaviour
    {
#if UNITY_SERVICES
        [Header("Unity Auth Panels")]
        [SerializeField] private SignInUI signInPanel;
        [SerializeField] private SignUpUI signUpPanel;
        [SerializeField] private SetPlayerProfileUI profileSetupPanel;
#endif

        [Header("Loading & Error")]
        [SerializeField] private RectTransform loadingPanel;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private RectTransform errorPanel;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private UnityEngine.UI.Button retryButton;

        public static AuthenticationUI Instance { get; private set; }

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            canvasGroup = GetComponent<CanvasGroup>();

            // Initial state
            HideAll();
            ShowLoading("Signing in...");

            // Subscribe to manager events
            AuthenticationManager.OnAuthStarted += OnAuthStarted;
            AuthenticationManager.OnAuthCompleted += OnAuthCompleted;
            AuthenticationManager.OnAuthError += OnAuthError;
#if UNITY_SERVICES
            AuthenticationManager.OnShowSignInUI += OnShowSignIn;
            AuthenticationManager.OnShowProfileSetupUI += OnShowProfileSetup;
#endif

            // Retry button
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnAuthStarted -= OnAuthStarted;
            AuthenticationManager.OnAuthCompleted -= OnAuthCompleted;
            AuthenticationManager.OnAuthError -= OnAuthError;
#if UNITY_SERVICES
            AuthenticationManager.OnShowSignInUI -= OnShowSignIn;
            AuthenticationManager.OnShowProfileSetupUI -= OnShowProfileSetup;
#endif
        }

        #region Event Handlers

        private void OnAuthStarted()
        {
            SetCanvasGroupAlpha(1f);
            HideAll();
            ShowLoading("Signing in...");
        }

        private void OnAuthCompleted()
        {
            HideAll();
            SetCanvasGroupAlpha(0f);
        }

        private void OnAuthError(string message)
        {
            SetCanvasGroupAlpha(1f);
            HideAll();
            ShowError(message);
        }

        private void SetCanvasGroupAlpha(float alpha)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = alpha > 0.9f;
            canvasGroup.blocksRaycasts = alpha > 0.9f;
        }

#if UNITY_SERVICES
        private void OnShowSignIn()
        {
            HideAll();
            signInPanel?.Show();
        }

        private void OnShowProfileSetup()
        {
            SetCanvasGroupAlpha(1f);
            HideAll();
            profileSetupPanel?.Show();
        }
#endif

        #endregion

        #region Public Methods (for child UIs)

        public void ShowLoadingForSignIn()
        {
            HideAll();
            ShowLoading("Signing in...");
        }

        public void ShowLoadingForSignUp()
        {
            HideAll();
            ShowLoading("Creating account...");
        }

#if UNITY_SERVICES
        public void ChangeToSignIn()
        {
            HideAll();
            signInPanel?.Show();
        }

        public void ChangeToSignUp()
        {
            HideAll();
            signUpPanel?.Show();
        }
#endif

        #endregion

        #region Private Helpers

        private void ShowLoading(string text)
        {
            if (loadingText != null) loadingText.text = text;
            loadingPanel?.gameObject.SetActive(true);
        }

        private void ShowError(string message)
        {
            if (errorText != null) errorText.text = message;
            errorPanel?.gameObject.SetActive(true);
        }

        private void HideAll()
        {
            loadingPanel?.gameObject.SetActive(false);
            errorPanel?.gameObject.SetActive(false);
#if UNITY_SERVICES
            signInPanel?.Hide();
            signUpPanel?.Hide();
            profileSetupPanel?.Hide();
#endif
        }

        private async void OnRetryClicked()
        {
            var authService = ServiceLocator.Get<IAuthService>();
            if (authService != null)
            {
                await authService.RetryAuthAsync();
            }
        }

        #endregion
    }
}