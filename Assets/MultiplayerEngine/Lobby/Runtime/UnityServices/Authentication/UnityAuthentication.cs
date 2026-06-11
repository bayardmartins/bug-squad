#if UNITY_SERVICES
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Unity Authentication integration for user sign-in, sign-up, and session management.
    /// </summary>
    public class UnityAuthentication : IAuthentication
    {
        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            await UnityServices.InitializeAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> AutoSignInAsync()
        {
            // Attempt to restore existing session if token exists
            if (AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    // SignInAnonymouslyAsync() will use cached session token if available
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[UnityAuth] Auto sign-in failed: " + ex);
                    return false;
                }
            }
            Debug.LogWarning("[UnityAuth] No session token found for auto sign-in.");
            return false;
        }

        /// <inheritdoc/>
        public async Task<bool> SignUpAsync(string email, string password, bool rememberMe)
        {
            try
            {
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email, password);

                // Clear session if user doesn't want to be remembered (must be AFTER sign-up)
                if (!rememberMe)
                {
                    try
                    {
                        AuthenticationService.Instance.ClearSessionToken();
                    }
                    catch (System.Exception ex)
                    {
                        // Don't fail the sign-up if clearing token fails
                        Debug.LogWarning("[UnityAuth] Failed to clear session token after sign-up: " + ex.Message);
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[UnityAuth] Sign-up failed for " + email + ": " + ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SignInAsync(string email, string password, bool rememberMe)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);

                // Clear session if user doesn't want to be remembered (must be AFTER sign-in)
                if (!rememberMe)
                {
                    try
                    {
                        AuthenticationService.Instance.ClearSessionToken();
                    }
                    catch (System.Exception ex)
                    {
                        // Don't fail the sign-in if clearing token fails
                        Debug.LogWarning("[UnityAuth] Failed to clear session token after sign-in: " + ex.Message);
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[UnityAuth] Sign-in failed for " + email + ": " + ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SignInWithCodeAsync(string authCode)
        {
            Debug.LogError("SignInWithCodeAsync is not implemented for Unity Authentication.");
            await Task.CompletedTask;
            return false;
        }

        /// <inheritdoc/>
        public Task<bool> SignOutAsync()
        {
            AuthenticationService.Instance.ClearSessionToken();
            try
            {
                AuthenticationService.Instance.SignOut();
                return Task.FromResult(true);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Sign-out failed: " + ex);
                return Task.FromResult(false);
            }
        }
    }
}
#endif