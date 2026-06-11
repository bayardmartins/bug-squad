using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents player statistics, including basic information and a flexible dictionary for custom attributes.
    /// </summary>
    [System.Serializable]
    public class PlayerStats
    {
        /// <summary>
        /// The unique identifier for the player.
        /// </summary>
        public string PlayerId;

        /// <summary>
        /// The display name of the player.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// The avatar image of the player.
        /// </summary>
        public Sprite PlayerAvatar;

        /// <summary>
        /// Flexible attributes for any additional stats.
        /// </summary>
        public Dictionary<string, string> CustomStats = new Dictionary<string, string>();
    }
}
