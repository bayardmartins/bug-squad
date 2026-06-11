#if STEAM_SERVICES
using Steamworks;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Steam-based authentication for multiplayer services.
    /// </summary>
    public class SteamAuthentication : IAuthentication
    {
        /// <inheritdoc/>
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<bool> AutoSignInAsync()
        {
            try
            {
                // SteamAPI.Init() is synchronous; wrap in Task.Run for async compatibility
                bool initialized = await Task.Run(() => SteamAPI.Init());
                if (!initialized)
                {
                    Debug.LogWarning("[SteamAuthentication] SteamAPI.Init() failed. Trying to launch Steam client...");

                    TryToOpenSteam();

                    await Task.Delay(10000); // Wait 10 seconds before retrying
                    initialized = await Task.Run(() => SteamAPI.Init());
                    if (!initialized)
                    {
                        Debug.LogWarning("[SteamAuthentication] SteamAPI.Init() failed after retry.");
                    }
                }
                return initialized;
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogWarning("[SteamAuthentication] Could not load steam_api.dll: " + e);
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SteamAuthentication] Unexpected exception during AutoSignInAsync: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Attempts to launch the Steam client using protocol or default install path.
        /// </summary>
        private void TryToOpenSteam()
        {
            try
            {
                // Method 1: Use steam:// protocol (works if Windows has Steam installed correctly)
                System.Diagnostics.Process.Start("steam://open/main");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SteamAuthentication] Failed to launch Steam via protocol: " + ex);
            }

            // Method 2 (fallback): Launch steam.exe from default path
            const string steamPath = @"C:\Program Files (x86)\Steam\Steam.exe";
            if (File.Exists(steamPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(steamPath);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[SteamAuthentication] Failed to launch Steam via default path: " + ex);
                }
            }
            else
            {
                Debug.LogWarning("[SteamAuthentication] Steam.exe not found at default path.");
            }
        }

        /// <inheritdoc/>
        public Task<bool> SignInAsync(string email, string password, bool rememberMe)
        {
            Debug.LogWarning("[SteamAuthentication] SignInAsync called, but not supported for Steam. Returning true.");
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<bool> SignInWithCodeAsync(string authCode)
        {
            Debug.LogWarning("[SteamAuth] SignInWithCodeAsync not supported for Steam.");
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> SignOutAsync()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<bool> SignUpAsync(string email, string password, bool rememberMe)
        {
            Debug.LogWarning("[SteamAuthentication] SignUpAsync called, but not supported for Steam. Returning true.");
            return Task.FromResult(true);
        }
    }
}
#endif