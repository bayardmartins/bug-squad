using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// General lobby manager — classic lobby with character selection, ready checks, and host-started games.
    /// Extends LobbyManagerBase for shared logic.
    /// </summary>
    public class LobbyManager : LobbyManagerBase
    {
        public static LobbyManager Instance { get; private set; }

        public override LobbyType LobbyType => LobbyType.General;

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            base.Awake();
        }

        protected override void InitializePlatformService()
        {
#if UNITY_SERVICES
            lobbyService = new UnityLobby();
#elif STEAM_SERVICES
            lobbyService = new SteamLobby();
#endif
        }
    }
}