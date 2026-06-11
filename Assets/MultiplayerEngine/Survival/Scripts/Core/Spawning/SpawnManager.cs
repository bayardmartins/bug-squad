#if UNITY_SERVICES || STEAM_SERVICES
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    public class SpawnManager : NetworkBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        private Dictionary<ulong, NetworkObject> spawnedCharacters = new Dictionary<ulong, NetworkObject>();

        private int spawnIndex = 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Server-side: Listen for disconnects to clean up spawned characters, and spawn characters when loaded
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += DespawnCharacterForClient;
                SessionManager.OnAllPlayersLoaded += SpawnAllCharacters;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer)
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= DespawnCharacterForClient;
                }
                SessionManager.OnAllPlayersLoaded -= SpawnAllCharacters;
            }
        }

        private void SpawnAllCharacters()
        {
            if (!IsServer) return;

            if (SessionManager.Instance == null)
            {
                Debug.LogError("[SpawnManager] SessionManager.Instance is null. Cannot spawn characters.");
                return;
            }

            foreach (var kvp in SessionManager.Instance.ServerSessionCache)
            {
                ulong clientId = kvp.Key;
                PlayerSessionData playerData = kvp.Value;

                if (spawnedCharacters.ContainsKey(clientId))
                {
                    Debug.LogWarning($"[SpawnManager] Client {clientId} already has a spawned character.");
                    continue;
                }

                if (string.IsNullOrEmpty(playerData.SelectedCharacterId))
                {
                     Debug.LogError($"[SpawnManager] Could not determine CharacterID for Client {clientId} (Player {playerData.PlayerId}). Character not specified.");
                     continue;
                }

                Transform spawnPoint = (spawnPoints.Length > 0)
                    ? spawnPoints[spawnIndex]
                    : null;

                Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
                Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

                CharacterData characterData = PlayerProfileManager.Instance.CharacterData.FirstOrDefault(c => c.CharacterId == playerData.SelectedCharacterId);
                
                if (characterData == null)
                {
                     Debug.LogError($"[SpawnManager] Character data not found for ID: {playerData.SelectedCharacterId}");
                     continue;
                }

                GameObject newCharacter = Instantiate(characterData.CharacterPrefab, spawnPosition, spawnRotation);
                NetworkObject networkObject = newCharacter.GetComponent<NetworkObject>();

                networkObject.SpawnAsPlayerObject(clientId);
                spawnedCharacters[clientId] = networkObject;

                // Move to the next spawn point, loop back if at the end
                spawnIndex = (spawnIndex + 1) % spawnPoints.Length;

                Debug.Log($"[SpawnManager] Authoritatively spawned character {playerData.SelectedCharacterId} for Client {clientId} (Player {playerData.PlayerId})");
            }
        }

        private void DespawnCharacterForClient(ulong clientId)
        {
            if (IsServer && spawnedCharacters.TryGetValue(clientId, out NetworkObject character))
            {
                character.Despawn();
                spawnedCharacters.Remove(clientId);
            }
        }
    }
}
#endif