using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handles the sign-up UI logic. Only functional in UNITY_SERVICES builds.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SignUpUI : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField username;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private TMP_InputField confirmPassword;

        [Header("Primary Actions")]
        [SerializeField] private Button signUp;

        [Header("Social Sign-Up")]
        [SerializeField] private Button google;
        [SerializeField] private Button apple;
        [SerializeField] private Button steam;

        [Header("Navigation")]
        [SerializeField] private Button switchToSignIn;

        [Header("Feedback")]
        [SerializeField] private TMP_Text error;

        private CanvasGroup canvasGroup;

#if UNITY_SERVICES
        private AuthenticationUI authenticationUI;
        private bool isSigningUp = false;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            authenticationUI = GetComponentInParent<AuthenticationUI>();

            signUp?.onClick.AddListener(OnSignUpClicked);
            switchToSignIn?.onClick.AddListener(() => authenticationUI?.ChangeToSignIn());
            google?.onClick.AddListener(OnGoogleSignUp);
            apple?.onClick.AddListener(OnAppleSignUp);
            steam?.onClick.AddListener(OnSteamSignUp);

            // Unsubscribe first to prevent duplicate subscriptions
            AuthenticationManager.OnSignUpCompleted -= OnSignUpFeedback;
            AuthenticationManager.OnSignUpCompleted += OnSignUpFeedback;

            username?.onValueChanged.AddListener(_ => ValidateInputs());
            password?.onValueChanged.AddListener(_ => ValidateInputs());
            confirmPassword?.onValueChanged.AddListener(_ => ValidateInputs());

            HideError();
            if (signUp != null) signUp.interactable = false;
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnSignUpCompleted -= OnSignUpFeedback;
        }

        #region Validation

        private void ValidateInputs()
        {
            bool valid = !string.IsNullOrEmpty(username?.text) &&
                         !string.IsNullOrEmpty(password?.text) &&
                         password.text.Length >= 8 &&
                         password.text == confirmPassword?.text;
            if (signUp != null) signUp.interactable = valid && !isSigningUp;
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

        private async void OnSignUpFeedback(bool success)
        {
            isSigningUp = false;

            if (!success)
            {
                // Hide loading panel and show sign-up UI
                authenticationUI?.ChangeToSignUp();
                ShowError("Sign up failed. This email may already be registered.");
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
                        await authService.OnManualSignUpSuccess();
                    }
                    else
                    {
                        Debug.LogError("[SignUpUI] No auth service registered!");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SignUpUI] OnManualSignUpSuccess failed: {ex.Message}");
                }
            }
        }

        private async void OnSignUpClicked()
        {
            if (isSigningUp) return;

            isSigningUp = true;
            if (signUp != null) signUp.interactable = false;
            HideError();

            Hide();
            AuthenticationUI.Instance?.ShowLoadingForSignUp();

            var authService = ServiceLocator.Get<IAuthService>();
            if (authService == null)
            {
                Show();
                ShowError("Authentication service unavailable.");
                isSigningUp = false;
                if (signUp != null) signUp.interactable = true;
                return;
            }

            await authService.SignUpAsync(username?.text?.Trim() ?? "", password?.text ?? "", false);
        }

        private void OnGoogleSignUp() { }
        private void OnAppleSignUp() { }
        private void OnSteamSignUp() { }

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
            if (confirmPassword != null) confirmPassword.text = string.Empty;
        }

        #endregion
#else
        public void Show() { }
        public void Hide() { }
#endif
    }
}
