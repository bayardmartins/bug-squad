using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Optional component that manages switching between First-Person and Third-Person perspectives.
    /// Handles camera priorities, mesh visibility, and custom enable/disable lists.
    /// 
    /// If this component is not present on the player, the PlayerController defaults to
    /// pure third-person mode. Remove this script (and your FP assets) to ship a TP-only game.
    /// </summary>
    public class PerspectiveSwitcher : NetworkBehaviour
    {
        [Header("Default Perspective")]
        [Tooltip("The perspective the player starts in")]
        public CameraPerspective DefaultPerspective = CameraPerspective.ThirdPerson;

        [Header("First Person")]
        [Tooltip("The follow target for the First Person Camera (usually placed at the head)")]
        public GameObject FirstPersonCameraTarget;

        [Tooltip("The actual First Person Cinemachine Camera component")]
        public CinemachineCamera FirstPersonVirtualCamera;

        [Tooltip("The mesh/rig to display in First Person view (e.g. FP arms)")]
        public GameObject FirstPersonMesh;

        [Header("Third Person")]
        [Tooltip("The mesh/rig to display in Third Person view (full body)")]
        public GameObject ThirdPersonMesh;

        [Header("Aim Camera")]
        [Tooltip("The over-the-shoulder aim camera (closer, offset to shoulder)")]
        public CinemachineCamera AimVirtualCamera;

        [Header("Camera Priorities")]
        [Tooltip("Priority of the active camera (higher = active)")]
        [SerializeField] private int activeCameraPriority = 20;

        [Tooltip("Priority of the inactive camera (lower = inactive)")]
        [SerializeField] private int inactiveCameraPriority = 10;

        [Header("Custom Object Lists")]
        [Tooltip("GameObjects to enable when entering First Person (and disable when leaving)")]
        public GameObject[] EnableOnFirstPerson;

        [Tooltip("GameObjects to disable when entering First Person (and enable when leaving)")]
        public GameObject[] DisableOnFirstPerson;

        // --- Public Properties ---

        /// <summary>The current active perspective.</summary>
        public CameraPerspective CurrentPerspective { get; private set; }

        /// <summary>Returns true when in First Person mode.</summary>
        public bool IsFirstPerson => CurrentPerspective == CameraPerspective.FirstPerson;

        /// <summary>
        /// Returns true when the player should use strafe movement (First Person OR Aiming).
        /// PlayerController queries this to decide movement style.
        /// </summary>
        public bool IsStrafeMode => IsFirstPerson || _isAiming;

        /// <summary>Returns the FP camera target for head-pitch rotation.</summary>
        public GameObject GetFirstPersonCameraTarget() => FirstPersonCameraTarget;

        // --- Private State ---
        private bool _isAiming;
        private IInputManager _input;
        private PlayerController _playerController;

        private void Awake()
        {
            CurrentPerspective = DefaultPerspective;
            _input = GetComponent<IInputManager>();
            _playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            // Unparent cameras so they aren't destroyed/moved with the player hierarchy
            if (FirstPersonVirtualCamera != null)
            {
                FirstPersonVirtualCamera.transform.SetParent(null);
            }
            if (AimVirtualCamera != null)
            {
                AimVirtualCamera.transform.SetParent(null);
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_input != null && _input.ToggleCameraView)
            {
                TogglePerspective();
            }
        }

        // --- Public API ---

        /// <summary>
        /// Toggles between First Person and Third Person.
        /// </summary>
        public void TogglePerspective()
        {
            CurrentPerspective = IsFirstPerson ? CameraPerspective.ThirdPerson : CameraPerspective.FirstPerson;
            UpdateCameraPriorities();
        }

        /// <summary>
        /// Sets the aiming state. When aiming in TP, switches to the aim camera.
        /// Called by PlayerController.
        /// </summary>
        public void SetAiming(bool aiming)
        {
            if (_isAiming == aiming) return;
            _isAiming = aiming;
            UpdateCameraPriorities();
        }

        /// <summary>
        /// Initializes camera and mesh state for the local owner player.
        /// Called by PlayerController during OnNetworkSpawn.
        /// </summary>
        public void InitializeForOwner(CinemachineCamera thirdPersonCamera)
        {
            // Enable the correct cameras based on starting perspective
            if (thirdPersonCamera != null && CurrentPerspective == CameraPerspective.ThirdPerson && !_isAiming)
            {
                thirdPersonCamera.enabled = true;
            }
            if (FirstPersonVirtualCamera != null && CurrentPerspective == CameraPerspective.FirstPerson)
            {
                FirstPersonVirtualCamera.enabled = true;
            }
            if (AimVirtualCamera != null && CurrentPerspective == CameraPerspective.ThirdPerson && _isAiming)
            {
                AimVirtualCamera.enabled = true;
            }

            UpdateCameraPriorities();
        }

        /// <summary>
        /// Sets up mesh state for non-owner players (always show TP body to others).
        /// Called by PlayerController during OnNetworkSpawn.
        /// </summary>
        public void InitializeForNonOwner()
        {
            if (FirstPersonMesh != null) FirstPersonMesh.SetActive(false);
            if (ThirdPersonMesh != null) ThirdPersonMesh.SetActive(true);

            // Disable virtual cameras on remote clients to prevent them from hijacking the Cinemachine Brain
            if (FirstPersonVirtualCamera != null)
            {
                FirstPersonVirtualCamera.enabled = false;
            }
            if (AimVirtualCamera != null)
            {
                AimVirtualCamera.enabled = false;
            }
        }

        // --- Private ---

        private void UpdateCameraPriorities()
        {
            if (!IsOwner) return;

            var thirdPersonCamera = _playerController != null ? _playerController.cinemachineVirtualCamera : null;

            if (IsFirstPerson)
            {
                // FP camera active, all TP cameras inactive
                if (FirstPersonVirtualCamera != null) FirstPersonVirtualCamera.Priority = activeCameraPriority;
                if (thirdPersonCamera != null) thirdPersonCamera.Priority = inactiveCameraPriority;
                if (AimVirtualCamera != null) AimVirtualCamera.Priority = inactiveCameraPriority;

                if (IsOwner)
                {
                    if (FirstPersonMesh != null) FirstPersonMesh.SetActive(true);
                    if (ThirdPersonMesh != null) ThirdPersonMesh.SetActive(false);
                    ApplyCustomLists(firstPerson: true);
                }
            }
            else // Third Person
            {
                if (FirstPersonVirtualCamera != null) FirstPersonVirtualCamera.Priority = inactiveCameraPriority;

                if (_isAiming)
                {
                    if (AimVirtualCamera != null) AimVirtualCamera.Priority = activeCameraPriority;
                    if (thirdPersonCamera != null) thirdPersonCamera.Priority = inactiveCameraPriority;
                }
                else
                {
                    if (thirdPersonCamera != null) thirdPersonCamera.Priority = activeCameraPriority;
                    if (AimVirtualCamera != null) AimVirtualCamera.Priority = inactiveCameraPriority;
                }

                if (IsOwner)
                {
                    if (FirstPersonMesh != null) FirstPersonMesh.SetActive(false);
                    if (ThirdPersonMesh != null) ThirdPersonMesh.SetActive(true);
                    ApplyCustomLists(firstPerson: false);
                }
            }
        }

        /// <summary>
        /// Enables/disables the custom object lists based on perspective.
        /// </summary>
        private void ApplyCustomLists(bool firstPerson)
        {
            if (EnableOnFirstPerson != null)
            {
                foreach (var obj in EnableOnFirstPerson)
                {
                    if (obj != null) obj.SetActive(firstPerson);
                }
            }
            if (DisableOnFirstPerson != null)
            {
                foreach (var obj in DisableOnFirstPerson)
                {
                    if (obj != null) obj.SetActive(!firstPerson);
                }
            }
        }
    }
}