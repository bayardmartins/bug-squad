namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Presence update event data for decoupled presence handling.
    /// </summary>
    public struct PresenceUpdateRequest
    {
        public FriendPresence RequestedPresence;
        public string Reason;

        public PresenceUpdateRequest(FriendPresence presence, string reason = null)
        {
            RequestedPresence = presence;
            Reason = reason;
        }
    }
}
