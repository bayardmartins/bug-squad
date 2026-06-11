using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Auto-despawn for network objects after a set lifetime.
    /// Add this to Pickable prefabs used as resource drops.
    /// </summary>
    public class AutoDespawn : NetworkBehaviour
    {
        [SerializeField] private float lifetime = 60f;
        
        private float spawnTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (!IsServer) return;

            if (Time.time - spawnTime > lifetime)
            {
                if (IsSpawned)
                    NetworkObject.Despawn(true);
            }
        }
    }
}