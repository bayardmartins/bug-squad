using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handles player interaction with world objects (pickup, interact, ping).
    /// Uses screen-center raycast with sphere overlap for detection.
    /// Updates centralized InteractionUI instead of per-object UI.
    /// </summary>
    public class InteractionController : NetworkBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Maximum distance for interaction raycast")]
        [SerializeField] private float maxInteractDistance = 5f;

        [Tooltip("Radius for sphere overlap to detect nearby interactables")]
        [SerializeField] private float pickupRadius = 0.8f;

        [Header("Layer Settings")]
        [Tooltip("Layers the raycast can HIT (ground, walls, etc.) to find where you're looking")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Layers to DETECT as interactables (pickups, doors, etc.)")]
        [SerializeField] private LayerMask detectLayers = ~0;

        private IInputManager inputManager;

        [Tooltip("Reference to the centralized interaction UI")]
        [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("interactionHUD")] private InteractionUI interactionUI;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private IInventoryManager inventoryManager;
        private Interactable currentInteractable;
        private Camera mainCamera;
        private bool hudWarningShown = false;
        private float lastPingTime = -999f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            inventoryManager = GetComponent<IInventoryManager>();
            mainCamera = Camera.main;

            if (inputManager == null)
                inputManager = GetComponent<IInputManager>();

        }

        private void Start()
        {
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Ensure camera reference is valid
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // Try to find UI if not assigned (check each frame until found)
            if (interactionUI == null)
            {
                interactionUI = InteractionUI.Instance;
                if (interactionUI == null && !hudWarningShown)
                {
                    // Always show this warning (not just debug mode)
                    Debug.LogWarning("[InteractionController] InteractionUI not found! Please create an InteractionUI UI element.");
                    hudWarningShown = true;
                }
            }

            // Find closest interactable using screen-center detection
            var closestInteractable = FindClosestInteractable();


            UpdateInteractionUI(closestInteractable);
            HandleInteractInput(closestInteractable);
            HandlePingInput(closestInteractable);
        }

        #region Screen-Center Detection

        /// <summary>
        /// Find the closest interactable using:
        /// 1. Raycast on hitLayers to find WHERE we're looking (ground, walls, etc.)
        /// 2. Sphere overlap on detectLayers to find interactables near that point
        /// </summary>
        private Interactable FindClosestInteractable()
        {
            // Cast ray from screen center
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

            // Combined layers for initial raycast (can hit surfaces OR directly hit interactables)
            LayerMask combinedLayers = hitLayers | detectLayers;

            Vector3 overlapCenter;
            float hitDistance = maxInteractDistance;

            // Raycast to find where we're looking
            if (Physics.Raycast(ray, out RaycastHit rayHit, maxInteractDistance, combinedLayers))
            {
                hitDistance = rayHit.distance;
                overlapCenter = rayHit.point;



                // Check if we directly hit an interactable
                Interactable directHit = rayHit.collider.GetComponentInParent<Interactable>();
                if (directHit != null)
                {
                    return directHit;
                }
            }
            else
            {
                // Nothing hit - place overlap center at max distance
                overlapCenter = ray.GetPoint(maxInteractDistance);
            }

            // Sphere overlap at hit point to find nearby interactables (using detectLayers only)
            Collider[] nearby = Physics.OverlapSphere(overlapCenter, pickupRadius, detectLayers);



            Interactable closest = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in nearby)
            {
                Interactable interactable = collider.GetComponentInParent<Interactable>();
                if (interactable != null)
                {
                    // Prefer objects closest to the actual hit point
                    float distance = Vector3.Distance(overlapCenter, interactable.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = interactable;
                    }
                }
            }

            return closest;
        }

        #endregion

        #region UI Updates

        private void UpdateInteractionUI(Interactable interactable)
        {
            // Update UI
            if (interactionUI != null)
            {
                if (interactable != null)
                {
                    // Get icon from the interactable object
                    Sprite icon = interactable.GetIcon();

                    interactionUI.Show(
                        interactable.InteractionType,
                        interactable.DisplayName,
                        interactable.GetDescription(),
                        interactable.GetInteractionInfo(),
                        icon
                    );
                }
                else
                {
                    interactionUI.Hide();
                }
            }

            // Track current interactable for state management
            currentInteractable = interactable;
        }

        #endregion

        #region Input Handling

        private void HandleInteractInput(Interactable interactable)
        {
            if (interactable == null) return;

            if (inputManager != null && inputManager.Interact)
            {
                ServerInteractRpc(interactable.NetworkObjectId);
                // interact flag is auto-cleared in InputManager.LateUpdate
            }
        }

        private void HandlePingInput(Interactable interactable)
        {
            if (inputManager == null || !inputManager.Ping) return;
            if (interactable == null) return;

            // Cooldown check (setting lives on MarkerManager)
            float cooldown = MarkerManager.Instance != null ? MarkerManager.Instance.PingCooldown : 1f;
            if (Time.time - lastPingTime < cooldown) return;

            lastPingTime = Time.time;

            string displayName = interactable.DisplayName;

            // Get the NetworkObject so all clients can find the same object
            var netObj = interactable.GetComponent<NetworkObject>();
            if (netObj == null)
                netObj = interactable.GetComponentInParent<NetworkObject>();

            if (netObj != null)
            {
                ServerPingObjectRpc(netObj.NetworkObjectId, displayName);
            }
            else
            {
                // Non-networked interactable — send world position
                ServerPingPositionRpc(interactable.transform.position, displayName);
            }
        }

        #endregion

        #region Network

        [Rpc(SendTo.Server)]
        private void ServerInteractRpc(ulong objectId, RpcParams rpcParams = default)
        {
            
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var netObj))
            {
                Debug.LogWarning($"[InteractionController] Object {objectId} not found in SpawnedObjects");
                return;
            }

            if (netObj == null)
            {
                Debug.LogWarning($"[InteractionController] NetObj is null");
                return;
            }

            var pickable = netObj.GetComponent<Pickable>();
            var interactable = netObj.GetComponent<Interactable>();

            // Non-pickable interactable (doors, buttons, etc.)
            if (interactable != null && pickable == null)
            {
                // Special handling for CraftingTable - needs clientId
                var craftingTable = interactable as CraftingTable;
                if (craftingTable != null)
                {
                    ulong clientId = rpcParams.Receive.SenderClientId;
                    craftingTable.TryInteract(clientId);
                    return;
                }

                interactable.Interact();
                return;
            }

            // Pickable item
            if (pickable != null)
            {
                // Skip if already parented (e.g., held in hand)
                // Skip if already parented to a PLAYER (held in hand)
                // We allow parents for scene organization (e.g. "Items" folder)
                if (netObj.transform.parent != null)
                {
                    var parentNetObj = netObj.transform.parent.GetComponentInParent<NetworkObject>();
                    if (parentNetObj != null && parentNetObj.GetComponent<PlayerController>() != null)
                    {
                        return;
                    }
                }

                if (inventoryManager == null)
                {
                    Debug.LogWarning("InteractionController: InventoryManager not found");
                    return;
                }

                if (inventoryManager.TryAddToInventory(pickable))
                {
                    pickable.Interact(); // Destroys the object
                }
                else
                {
                }
            }
        }

        #endregion

        #region Ping Network

        [Rpc(SendTo.Server)]
        private void ServerPingObjectRpc(ulong networkObjectId, string displayName)
        {
            ClientPingObjectRpc(networkObjectId, displayName);
        }

        [Rpc(SendTo.Server)]
        private void ServerPingPositionRpc(Vector3 worldPos, string displayName)
        {
            ClientPingPositionRpc(worldPos, displayName);
        }

        [Rpc(SendTo.Everyone)]
        private void ClientPingObjectRpc(ulong networkObjectId, string displayName)
        {
            if (MarkerManager.Instance == null) return;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
                return;
            if (netObj == null) return;

            // Get icon if it's an Interactable
            Sprite icon = null;
            var interactable = netObj.GetComponent<Interactable>();
            if (interactable != null)
                icon = interactable.GetIcon();

            MarkerManager.Instance.PingAt(
                netObj.transform,
                netObj.transform.position,
                displayName,
                icon
            );
        }

        [Rpc(SendTo.Everyone)]
        private void ClientPingPositionRpc(Vector3 worldPos, string displayName)
        {
            if (MarkerManager.Instance == null) return;

            MarkerManager.Instance.PingAt(
                null,
                worldPos,
                displayName
            );
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Draw raycast line
            Gizmos.color = Color.cyan;
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            Gizmos.DrawRay(ray.origin, ray.direction * maxInteractDistance);

            // Draw pickup sphere at typical detection distance
            Gizmos.color = Color.yellow;
            Vector3 sphereCenter = ray.GetPoint(maxInteractDistance * 0.5f);
            Gizmos.DrawWireSphere(sphereCenter, pickupRadius);
        }

        #endregion
    }
}