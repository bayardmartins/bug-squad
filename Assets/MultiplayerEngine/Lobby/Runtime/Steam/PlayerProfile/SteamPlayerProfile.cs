#if STEAM_SERVICES
using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Steam-specific implementation for player profile operations.
    /// </summary>
    public class SteamPlayerProfile : IPlayerProfile
    {
        /// <inheritdoc />
        public async Task<PlayerStats> GetPlayerDataAsync(IList<string> customStats = null)
        {
            var richPresence = new Dictionary<string, string>();

            // Retrieve custom rich presence stats if provided
            if (customStats != null)
            {
                foreach (var stat in customStats)
                {
                    richPresence[stat] = Steamworks.SteamFriends.GetFriendRichPresence(SteamUser.GetSteamID(), stat);
                }
            }

            var profileData = new PlayerStats
            {
                PlayerId = SteamUser.GetSteamID().ToString(),
                DisplayName = Steamworks.SteamFriends.GetPersonaName(),
                PlayerAvatar = GetFriendAvatar(SteamUser.GetSteamID()),
                CustomStats = richPresence
            };
            return await Task.FromResult(profileData);
        }

        /// <inheritdoc />
        public async Task<PlayerStats> GetRemotePlayerDataAsync(string friendId, IList<string> customStats = null)
        {
            var richPresence = new Dictionary<string, string>();

            // Retrieve custom rich presence stats for remote player if provided
            if (customStats != null)
            {
                foreach (var stat in customStats)
                {
                    richPresence[stat] = Steamworks.SteamFriends.GetFriendRichPresence(SteamUser.GetSteamID(), stat);
                }
            }

            var steamId = new CSteamID(ulong.Parse(friendId));

            var profileData = new PlayerStats
            {
                PlayerId = steamId.ToString(),
                DisplayName = Steamworks.SteamFriends.GetFriendPersonaName(steamId),
                PlayerAvatar = GetFriendAvatar(steamId),
                CustomStats = richPresence
            };

            return await Task.FromResult(profileData);
        }

        /// <summary>
        /// Retrieves the avatar sprite for a given Steam user.
        /// </summary>
        /// <param name="steamId">Steam user ID.</param>
        /// <returns>Avatar sprite if available; otherwise, null.</returns>
        public static Sprite GetFriendAvatar(CSteamID steamId)
        {
            // Use medium avatar size (1)
            int imageId = Steamworks.SteamFriends.GetMediumFriendAvatar(steamId);

            if (imageId == -1)
                return null; // Avatar not loaded

            if (!SteamUtils.GetImageSize(imageId, out uint width, out uint height))
                return null;

            var image = new byte[width * height * 4]; // RGBA format
            if (!SteamUtils.GetImageRGBA(imageId, image, image.Length))
                return null;

            var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(image);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        public Task<(bool userName, bool avatar)> UpdatePlayerProfileAsync(string displayName, string avatarId)
        {
            Debug.LogWarning("Steam does not support updating player profile data via the API.");
            return Task.FromResult((false, false));
        }

        /// <inheritdoc />
        public Task<bool> SetCustomStatAsync(string key, string value)
        {
            // Steam stores custom data via Rich Presence
            Steamworks.SteamFriends.SetRichPresence(key, value);
            return Task.FromResult(true);
        }
    }
}
#endif