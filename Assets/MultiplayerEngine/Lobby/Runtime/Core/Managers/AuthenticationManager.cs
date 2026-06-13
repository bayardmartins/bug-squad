using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages user authentication flow. Handles the complete auth lifecycle:
    /// - Auto sign-in attempt on start
    /// - Manual sign-in/sign-up (Unity only)
    /// - Profile check
    /// - Fires events that UI can subscribe to
    /// </summary>
    public class AuthenticationManager : MonoBehaviour, IAuthService
    {
        public static AuthenticationManager Instance { get; private set; }

        private IAuthentication authenticationProvider;

        // Flow events - UI subscribes to these
        public static event System.Action OnAuthStarted;
        public static event System.Action OnAuthCompleted;
        public static event System.Action<string> OnAuthError;
        public static event System.Action OnShowSignInUI;      // Unity only
        public static event System.Action OnShowProfileSetupUI; // Unity only

        // Legacy events for child UIs
        public static event System.Action<bool> OnSignInCompleted;
        public static event System.Action<bool> OnSignUpCompleted;
        public static event System.Action<bool> OnSignOutCompleted;
        public static event System.Action<bool> OnProfileSetupCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_SERVICES
            authenticationProvider = new UnityAuthentication();
#elif STEAM_SERVICES
            authenticationProvider = new SteamAuthentication();
#endif

            ServiceLocator.Register<IAuthService>(this);
        }

        private async void Start()
        {
            await RunAuthFlow();
        }

        /// <summary>
        /// Main authentication flow. Called on start and by retry.
        /// </summary>
        private async Task RunAuthFlow()
        {
            if (authenticationProvider == null)
            {
                OnAuthError?.Invoke("Authentication provider not configured.");
                return;
            }

            OnAuthStarted?.Invoke();

            // Step 1: Initialize
            await authenticationProvider.InitializeAsync();

            // Step 2: Auto sign-in
            bool signedIn = await authenticationProvider.AutoSignInAsync();

            if (!signedIn)
            {
#if STEAM_SERVICES
                // Steam failed - show error (no manual sign-in possible)
                OnAuthError?.Invoke("Steam is not running.\nPlease start Steam and restart the game.");
#else
                // Unity - show sign-in UI
                OnShowSignInUI?.Invoke();
#endif
                return;
            }

            // Step 3: Check profile
            await CheckProfileAndComplete();
        }

        /// <summary>
        /// Called after successful sign-in/sign-up to check profile.
        /// </summary>
        private async Task CheckProfileAndComplete()
        {
            try
            {
                bool hasProfile = await CheckForPlayerProfile();

                if (hasProfile)
                {
                    OnAuthCompleted?.Invoke();
                }
                else
                {
#if UNITY_SERVICES
                    OnShowProfileSetupUI?.Invoke();
#elif STEAM_SERVICES
                    // Steam handles profile automatically
                    OnAuthCompleted?.Invoke();
#endif
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AuthManager] CheckProfileAndComplete failed: {ex.Message}. Completing auth anyway.");
                // Still complete auth to avoid getting stuck
                OnAuthCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Retry authentication. Called by UI retry button.
        /// </summary>
        public async Task RetryAuthAsync()
        {
            await RunAuthFlow();
        }

        /// <summary>
        /// Called by SignInUI after successful sign-in.
        /// </summary>
        public async Task OnManualSignInSuccess()
        {
            try
            {
                await CheckProfileAndComplete();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AuthManager] OnManualSignInSuccess failed: {ex.Message}. Completing auth anyway.");
                OnAuthCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Called by SignUpUI after successful sign-up.
        /// </summary>
        public async Task OnManualSignUpSuccess()
        {
            try
            {
                await CheckProfileAndComplete();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AuthManager] OnManualSignUpSuccess failed: {ex.Message}. Completing auth anyway.");
                OnAuthCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Called by SetPlayerProfileUI after profile setup.
        /// </summary>
        public async Task OnProfileSetupSuccess()
        {
            bool hasProfile = await CheckForPlayerProfile();
            if (hasProfile)
            {
                OnAuthCompleted?.Invoke();
            }
        }

        #region Sign-In/Sign-Up Methods

        public async Task<bool> SignInAsync(string email, string password, bool rememberMe)
        {
            if (authenticationProvider == null) return false;

            bool result = await authenticationProvider.SignInAsync(email, password, rememberMe);
            OnSignInCompleted?.Invoke(result);
            return result;
        }

        public async Task<bool> SignUpAsync(string email, string password, bool rememberMe)
        {
            if (authenticationProvider == null) return false;

            bool result = await authenticationProvider.SignUpAsync(email, password, rememberMe);
            OnSignUpCompleted?.Invoke(result);
            return result;
        }

        public async Task<bool> SignOutAsync()
        {
            if (authenticationProvider == null) return false;

            bool result = await authenticationProvider.SignOutAsync();
            if (result && RuntimeSessionData.Exists)
            {
                RuntimeSessionData.Instance.ClearAll();
            }
            OnSignOutCompleted?.Invoke(result);
            return result;
        }

        #endregion

        #region Profile Check

        public async Task<bool> CheckForPlayerProfile()
        {
            // First try reading from RuntimeSessionData (decoupled path)
            if (RuntimeSessionData.Exists && RuntimeSessionData.Instance.IsAuthenticated)
            {
                bool isComplete = !string.IsNullOrEmpty(RuntimeSessionData.Instance.DisplayName);
                if (isComplete)
                {
                    OnProfileSetupCompleted?.Invoke(true);
                    return true;
                }
            }

            // Fallback: use profile service if available (decoupled via ServiceLocator)
            IProfileService profileService = ServiceLocator.Get<IProfileService>();
            if (profileService == null)
            {
                OnProfileSetupCompleted?.Invoke(true);
                return true;
            }

            var stats = await profileService.GetLocalPlayerStatsAsync(null);

            if (stats == null)
            {
                OnProfileSetupCompleted?.Invoke(false);
                return false;
            }

            bool hasProfile = !string.IsNullOrEmpty(stats.DisplayName);
            OnProfileSetupCompleted?.Invoke(hasProfile);
            return hasProfile;
        }

        #endregion
    }
}