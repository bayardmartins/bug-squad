#if UNITY_SERVICES
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Unity Services implementation for player profile operations.
    /// </summary>
    public class UnityPlayerProfile : IPlayerProfile
    {
        /// <inheritdoc />
        public async Task<PlayerStats> GetPlayerDataAsync(IList<string> customStat = null)
        {
            var profileStats = new PlayerStats
            {
                PlayerId = AuthenticationService.Instance.PlayerId,
                DisplayName = AuthenticationService.Instance.PlayerName,
                CustomStats = new Dictionary<string, string>()
            };

            // Prepare keys to load from Cloud Save
            var keysToLoad = customStat != null ? new HashSet<string>(customStat) : new HashSet<string>();
            keysToLoad.Add("avatarId");

            var loadOptions = new LoadOptions(new PublicReadAccessClassOptions(profileStats.PlayerId));
            var playerData = await CloudSaveService.Instance.Data.Player.LoadAsync(keysToLoad, loadOptions);

            // Populate PlayerStats with loaded data
            foreach (var kvp in playerData)
            {
                if (kvp.Key == "avatarId")
                {
                    profileStats.PlayerAvatar = PlayerProfileManager.Instance.GetIconById(kvp.Value.Value.GetAsString());
                    continue;
                }
                profileStats.CustomStats[kvp.Key] = kvp.Value.Value.GetAsString();
            }

            return profileStats;
        }

        /// <inheritdoc />
        public async Task<PlayerStats> GetRemotePlayerDataAsync(string friendId, IList<string> customStat = null)
        {
            var profileStats = new PlayerStats
            {
                PlayerId = friendId,
                DisplayName = FriendsManager.Instance?.FriendsList.Find(f => f.PlayerId == friendId)?.DisplayName ?? "Unknown",
                CustomStats = new Dictionary<string, string>()
            };

            // Prepare keys to load from Cloud Save for remote player
            var keysToLoad = customStat != null ? new HashSet<string>(customStat) : new HashSet<string>();
            keysToLoad.Add("avatarId");

            var loadOptions = new LoadOptions(new PublicReadAccessClassOptions(friendId));
            var playerData = await CloudSaveService.Instance.Data.Player.LoadAsync(keysToLoad, loadOptions);

            foreach (var kvp in playerData)
            {
                if (kvp.Key == "avatarId")
                {
                    profileStats.PlayerAvatar = PlayerProfileManager.Instance.GetIconById(kvp.Value.Value.GetAsString());
                    continue;
                }
                profileStats.CustomStats[kvp.Key] = kvp.Value.Value.GetAsString();
            }

            return profileStats;
        }

        /// <inheritdoc />
        public async Task<(bool userName, bool avatar)> UpdatePlayerProfileAsync(string displayName, string avatarId)
        {
            bool userNameSuccess = false;
            bool avatarSuccess = false;

            // Update display name
            try
            {
                string updatedName = await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
                userNameSuccess = true;
            }
            catch
            {
                userNameSuccess = false;
            }

            // Update avatarId in Cloud Save
            try
            {
                var data = new Dictionary<string, object> { { "avatarId", avatarId } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(
                    data,
                    new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions())
                );
                avatarSuccess = true;
            }
            catch
            {
                avatarSuccess = false;
            }

            // Return overall success as required by the interface
            return (userNameSuccess, avatarSuccess);
        }

        /// <inheritdoc />
        public async Task<bool> SetCustomStatAsync(string key, string value)
        {
            try
            {
                var data = new Dictionary<string, object> { { key, value } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(
                    data,
                    new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions())
                );
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif