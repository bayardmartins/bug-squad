using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handles the sign-in UI logic. Only functional in UNITY_SERVICES builds.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SignInUI : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField username;
        [SerializeField] private TMP_InputField password;

        [Header("Primary Actions")]
        [SerializeField] private Button signIn;

        [Header("Social Sign-In")]
        [SerializeField] private Button google;
        [SerializeField] private Button apple;
        [SerializeField] private Button steam;

        [Header("Navigation")]
        [SerializeField] private Button switchToSignUp;

        [Header("Feedback")]
        [SerializeField] private TMP_Text error;

        private CanvasGroup canvasGroup;

#if UNITY_SERVICES
        private AuthenticationUI authenticationUI;
        private bool isSigningIn = false;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            authenticationUI = GetComponentInParent<AuthenticationUI>();

            signIn?.onClick.AddListener(OnSignInClicked);
            switchToSignUp?.onClick.AddListener(() => authenticationUI?.ChangeToSignUp());
            google?.onClick.AddListener(OnGoogleSignIn);
            apple?.onClick.AddListener(OnAppleSignIn);
            steam?.onClick.AddListener(OnSteamSignIn);

            // Unsubscribe first to prevent duplicate subscriptions
            AuthenticationManager.OnSignInCompleted -= OnSignInFeedback;
            AuthenticationManager.OnSignInCompleted += OnSignInFeedback;

            username?.onValueChanged.AddListener(_ => ValidateInputs());
            password?.onValueChanged.AddListener(_ => ValidateInputs());

            HideError();
            if (signIn != null) signIn.interactable = false;
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnSignInCompleted -= OnSignInFeedback;
        }

        #region Validation

        private void ValidateInputs()
        {
            bool valid = !string.IsNullOrEmpty(username?.text) &&
                         !string.IsNullOrEmpty(password?.text) &&
                         password.text.Length >= 8;
            if (signIn != null) signIn.interactable = valid && !isSigningIn;
        }

        #endregion

        #region Show/Hide

        public void Show()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) return;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        #endregion

        #region Event Handlers

        private async void OnSignInFeedback(bool success)
        {
            isSigningIn = false;

            if (!success)
            {
                // Hide loading panel and show sign-in UI
                authenticationUI?.ChangeToSignIn();
                ShowError("Sign in failed. Please check your credentials.");
                ValidateInputs(); // Re-validate to properly set button interactable state
            }
            else
            {
                HideError();
                ClearInputs();
                try
                {
                    var authService = ServiceLocator.Get<IAuthService>();
                    if (authService != null)
                    {
                        await authService.OnManualSignInSuccess();
                    }
                    else
                    {
                        Debug.LogError("[SignInUI] No auth service registered!");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SignInUI] OnManualSignInSuccess failed: {ex.Message}");
                }
            }
        }

        private async void OnSignInClicked()
        {
            if (isSigningIn) return;

            isSigningIn = true;
            if (signIn != null) signIn.interactable = false;
            HideError();

            Hide();
            AuthenticationUI.Instance?.ShowLoadingForSignIn();

            var authService = ServiceLocator.Get<IAuthService>();
            if (authService == null)
            {
                Show();
                ShowError("Authentication service unavailable.");
                isSigningIn = false;
                if (signIn != null) signIn.interactable = true;
                return;
            }

            await authService.SignInAsync(username?.text?.Trim() ?? "", password?.text ?? "", false);
        }

        private void OnGoogleSignIn() { }
        private void OnAppleSignIn() { }
        private void OnSteamSignIn() { }

        #endregion

        #region Error Helpers

        private void ShowError(string message)
        {
            if (error != null)
            {
                error.text = message;
                error.gameObject.SetActive(true);
            }
        }

        private void HideError()
        {
            if (error != null) error.gameObject.SetActive(false);
        }

        private void ClearInputs()
        {
            if (username != null) username.text = string.Empty;
            if (password != null) password.text = string.Empty;
        }

        #endregion
#else
        // Non-Unity builds: empty stubs so component exists
        public void Show() { }
        public void Hide() { }
#endif
    }
}