using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    public class VoiceMemeberSettings
    {
        public string MemberId { get; set; }
        public int Volume { get; set; }
        public bool IsMuted { get; set; }
        public Sprite Sprite { get; set; }
        public string DisplayName { get; set; }
    }
}
