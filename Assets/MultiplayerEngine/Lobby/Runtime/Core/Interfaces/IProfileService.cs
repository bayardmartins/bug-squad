using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manager-level interface for player profile operations.
    /// Other managers and UI resolve this via ServiceLocator instead of using PlayerProfileManager.Instance.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>List of available player icons.</summary>
        List<Sprite> PlayerIcons { get; }

        /// <summary>List of available character data.</summary>
        List<CharacterData> CharacterData { get; }

        /// <summary>The local player's statistics.</summary>
        PlayerStats LocalPlayerStats { get; }

        Task<PlayerStats> GetLocalPlayerStatsAsync(IList<string> customStatRequest);
        Task<PlayerStats> GetRemotePlayerStatsAsync(string playerId, IList<string> customStatRequest = null);
        Task<PlayerStats> GetPlayerStatsAsync(string playerId = null, IList<string> customStatRequest = null);
        Task<(bool userName, bool avatar)> UpdatePlayerDataAsync(string displayName, string avatarId);
        Task<bool> SetCustomStatAsync(string key, string value);
        Sprite GetIconById(string iconId);
    }
}
