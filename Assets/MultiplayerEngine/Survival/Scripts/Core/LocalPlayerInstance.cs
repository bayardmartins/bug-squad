using System;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Thread-safe, central static class that acts as the single source of truth for the local player's spawn lifecycle in multiplayer.
    /// Eliminates the need for expensive FindFirstObjectByType or Update polling in UI and managers.
    /// </summary>
    public static class LocalPlayerInstance
    {
        private static PlayerController _localPlayer;

        /// <summary>
        /// Gets the current local player controller, or null if they haven't spawned yet.
        /// </summary>
        public static PlayerController LocalPlayer
        {
            get => _localPlayer;
            private set => _localPlayer = value;
        }

        /// <summary>
        /// True if the local player is currently spawned in the scene.
        /// </summary>
        public static bool IsSpawned => _localPlayer != null;

        // Cached subsystem references - fetched once on spawn to avoid GetComponent overhead
        private static InputManager _inputManager;
        private static InventoryManager _inventoryManager;
        private static EquipmentController _equipmentController;
        private static PlayerStatsManager _playerStatsManager;

        /// <summary>
        /// Static helper getter for the local player's InputManager.
        /// </summary>
        public static InputManager InputManager => _inputManager;

        /// <summary>
        /// Static helper getter for the local player's InventoryManager.
        /// </summary>
        public static InventoryManager InventoryManager => _inventoryManager;

        /// <summary>
        /// Static helper getter for the local player's EquipmentController.
        /// </summary>
        public static EquipmentController EquipmentController => _equipmentController;

        /// <summary>
        /// Static helper getter for the local player's PlayerStatsManager.
        /// </summary>
        public static PlayerStatsManager PlayerStatsManager => _playerStatsManager;

        // Static events that trigger when the local player is spawned or despawned
        public static event Action<PlayerController> OnLocalPlayerSpawned;
        public static event Action OnLocalPlayerDespawned;

        /// <summary>
        /// Registers a PlayerController as the local player. Called by PlayerController during OnNetworkSpawn when IsOwner is true.
        /// </summary>
        public static void Register(PlayerController player)
        {
            if (player == null) return;

            // Handle clean override or double registration if necessary
            if (_localPlayer != null && _localPlayer != player)
            {
                Debug.LogWarning("[LocalPlayerInstance] Overriding existing local player reference. Make sure the previous local player unregistered correctly.");
                Unregister();
            }

            _localPlayer = player;
            
            // Cache components to optimize property reads
            _inputManager = player.GetComponent<InputManager>();
            _inventoryManager = player.GetComponent<InventoryManager>();
            _equipmentController = player.GetComponent<EquipmentController>();
            _playerStatsManager = player.GetComponent<PlayerStatsManager>();

            Debug.Log($"[LocalPlayerInstance] Local player registered: {player.name} (Owner Client ID: {player.OwnerClientId})");

            OnLocalPlayerSpawned?.Invoke(player);
        }

        /// <summary>
        /// Unregisters the local player. Called by PlayerController during OnNetworkDespawn when IsOwner is true.
        /// </summary>
        public static void Unregister()
        {
            if (_localPlayer == null) return;

            Debug.Log("[LocalPlayerInstance] Local player unregistered.");
            
            _localPlayer = null;
            _inputManager = null;
            _inventoryManager = null;
            _equipmentController = null;
            _playerStatsManager = null;

            OnLocalPlayerDespawned?.Invoke();
        }
    }
}