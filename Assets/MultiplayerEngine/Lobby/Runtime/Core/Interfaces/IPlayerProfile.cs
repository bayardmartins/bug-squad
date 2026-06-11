using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides an abstraction for accessing and updating player profile data and statistics.
    /// </summary>
    public interface IPlayerProfile
    {
        /// <summary>
        /// Retrieves the authenticated player's profile data and custom statistics.
        /// </summary>
        /// <param name="customStats">
        /// Optional list of custom statistic keys to retrieve. If null, all available stats are returned.
        /// </param>
        /// <returns>
        /// A <see cref="PlayerStats"/> instance containing the player's profile and requested statistics.
        /// </returns>
        Task<PlayerStats> GetPlayerDataAsync(IList<string> customStats = null);

        /// <summary>
        /// Retrieves the profile data and custom statistics for a remote player (e.g., a friend).
        /// </summary>
        /// <param name="friendId">The unique identifier of the remote player.</param>
        /// <param name="customStats">
        /// Optional list of custom statistic keys to retrieve. If null, all available stats are returned.
        /// </param>
        /// <returns>
        /// A <see cref="PlayerStats"/> instance containing the remote player's profile and requested statistics.
        /// </returns>
        Task<PlayerStats> GetRemotePlayerDataAsync(string friendId, IList<string> customStats = null);

        /// <summary>
        /// Updates the authenticated player's profile information.
        /// </summary>
        /// <param name="displayName">The new display name for the player.</param>
        /// <param name="avatarId">The identifier for the new avatar image.</param>
        /// <returns>
        /// True if the profile was updated successfully; otherwise, false.
        /// </returns>
        Task<(bool userName, bool avatar)> UpdatePlayerProfileAsync(string displayName, string avatarId);

        /// <summary>
        /// Sets a custom statistic for the authenticated player.
        /// </summary>
        /// <param name="key">The statistic key.</param>
        /// <param name="value">The statistic value.</param>
        /// <returns>True if the statistic was set successfully; otherwise, false.</returns>
        Task<bool> SetCustomStatAsync(string key, string value);
    }
}