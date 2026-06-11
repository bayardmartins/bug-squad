using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    public enum CameraPerspective { ThirdPerson, FirstPerson }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera horizontally")]
        public CinemachineCamera cinemachineVirtualCamera;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animMoveX;
        private float _animMoveY;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float rotateSensitivity = 1;
        private bool rotateOnMove = true;
        private bool _isAiming;
        private PerspectiveSwitcher _perspectiveSwitcher;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDMoveX;
        private int _animIDMoveY;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        private PlayerInput _playerInput;
        private Animator _animator;
        private CharacterController _controller;
        private IInputManager _input;
        private GameObject _mainCamera;
        private IPlayerStatsManager _statsManager;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        private bool _movementLocked;

        private bool IsCurrentDeviceMouse
        {
            get
            {
                return _playerInput.currentControlScheme == "KeyboardMouse";
            }
        }




        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                // Disable camera for non-owners
                if (cinemachineVirtualCamera != null)
                {
                    cinemachineVirtualCamera.enabled = false;
                }

                // Disable input for non-owners
                if (_playerInput != null)
                {
                    _playerInput.enabled = false;
                }
                if (_input != null)
                {
                    _input.enabled = false;
                }

                // Delegate mesh setup to PerspectiveSwitcher (if present)
                if (_perspectiveSwitcher != null)
                {
                    _perspectiveSwitcher.InitializeForNonOwner();
                }

                // Disable this script for non-owners to prevent local movement calculation
                enabled = false;
            }
            else
            {
                // Enable base TP camera for owner
                if (cinemachineVirtualCamera != null)
                {
                    cinemachineVirtualCamera.enabled = true;
                }

                // Delegate perspective-specific camera/mesh init to PerspectiveSwitcher (if present)
                if (_perspectiveSwitcher != null)
                {
                    _perspectiveSwitcher.InitializeForOwner(cinemachineVirtualCamera);
                }

                // Register this instance as the local player
                LocalPlayerInstance.Register(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                LocalPlayerInstance.Unregister();
            }
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                cinemachineVirtualCamera.Follow = CinemachineCameraTarget.transform;
            }

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<IInputManager>();
            _playerInput = GetComponent<PlayerInput>();
            _statsManager = GetComponent<IPlayerStatsManager>();
            _perspectiveSwitcher = GetComponent<PerspectiveSwitcher>();
        }

        private void Start()
        {
            // cinemachineVirtualCamera.enabled = true; // Handled in OnNetworkSpawn
             if (cinemachineVirtualCamera != null)
            {
               cinemachineVirtualCamera.transform.SetParent(null);
            }

            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDMoveX = Animator.StringToHash("MoveX");
            _animIDMoveY = Animator.StringToHash("MoveY");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.Look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.Look.x * deltaTimeMultiplier * rotateSensitivity;
                _cinemachineTargetPitch += _input.Look.y * deltaTimeMultiplier * rotateSensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            bool isFirstPerson = _perspectiveSwitcher != null && _perspectiveSwitcher.IsFirstPerson;

            if (!isFirstPerson)
            {
                // Third Person: Cinemachine will follow this target for orbit
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
            }
            else
            {
                // First Person Camera Rotation
                // Rotate the player horizontally (yaw) 
                transform.rotation = Quaternion.Euler(0.0f, _cinemachineTargetYaw, 0.0f);
                var fpTarget = _perspectiveSwitcher.GetFirstPersonCameraTarget();
                if (fpTarget != null)
                {
                    // Rotate the head vertically (pitch)
                    fpTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, 0.0f, 0.0f);
                }
            }
        }

        private void Move()
        {
            // Determine if we are in strafe mode (aiming or first person)
            bool isStrafeMode = _perspectiveSwitcher != null ? _perspectiveSwitcher.IsStrafeMode : _isAiming;

            // If movement is locked (e.g. during melee combo), zero out directional params
            if (_movementLocked)
            {
                if (_hasAnimator)
                {
                    _animator.SetFloat(_animIDMoveX, 0f);
                    _animator.SetFloat(_animIDMoveY, 0f);
                    _animator.SetFloat(_animIDMotionSpeed, 0f);
                }
                return;
            }

            // Disable sprint while aiming or when out of stamina
            bool canSprint = _input.Sprint && !_isAiming && (_statsManager == null || _statsManager.CanRun);
            float targetSpeed = canSprint ? SprintSpeed : MoveSpeed;

            // Notify stats manager of running state for stamina drain
            bool isRunning = canSprint && _input.Move != Vector2.zero;
            _statsManager?.SetRunning(isRunning);

            // if there is no input, set the target speed to 0
            if (_input.Move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.AnalogMovement ? _input.Move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.Move.x, 0.0f, _input.Move.y).normalized;

            // --- Compute directional MoveX / MoveY for blend tree ---
            // MoveX: negative = strafe left, positive = strafe right
            // MoveY: negative = walk backward, positive = walk forward
            // Speed (walk vs sprint) scales the magnitude so the blend tree
            // can differentiate walk clips from run clips.
            float targetMoveX = 0f;
            float targetMoveY = 0f;

            if (_input.Move != Vector2.zero)
            {
                // Scale factor: 1.0 at walk speed, higher when sprinting
                float speedScale = targetSpeed > 0.01f ? _speed / MoveSpeed : 0f;

                if (isStrafeMode)
                {
                    // Strafe / Aim / FPS: input axes map directly to local directions
                    targetMoveX = _input.Move.x * speedScale;
                    targetMoveY = _input.Move.y * speedScale;
                }
                else
                {
                    // Free-look 3rd person: character rotates to face movement,
                    // so movement is always "forward" relative to the character.
                    // MoveX stays 0, MoveY carries the full magnitude.
                    targetMoveX = 0f;
                    targetMoveY = inputMagnitude * speedScale;
                }
            }

            // Smooth the directional values to avoid animation snapping
            _animMoveX = Mathf.Lerp(_animMoveX, targetMoveX, Time.deltaTime * SpeedChangeRate);
            _animMoveY = Mathf.Lerp(_animMoveY, targetMoveY, Time.deltaTime * SpeedChangeRate);
            if (Mathf.Abs(_animMoveX) < 0.01f) _animMoveX = 0f;
            if (Mathf.Abs(_animMoveY) < 0.01f) _animMoveY = 0f;

            if (isStrafeMode)
            {
                // FPS MODE or TPS AIM MODE: Character always faces camera forward direction (strafe movement)
                float cameraYaw = _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, cameraYaw, ref _rotationVelocity,
                    RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

                // Movement is relative to camera (strafing)
                if (_input.Move != Vector2.zero)
                {
                    _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw;
                }
                else
                {
                    _targetRotation = cameraYaw;
                }
            }
            else if (_input.Move != Vector2.zero)
            {
                // NORMAL MODE: rotates toward movement direction
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                if (rotateOnMove)
                {
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDMoveX, _animMoveX);
                _animator.SetFloat(_animIDMoveY, _animMoveY);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.Jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                if (_input != null) _input.Jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        /// <summary>
        /// Locks player movement (e.g. during melee combo attacks).
        /// </summary>
        public void LockMovement()
        {
            _movementLocked = true;
        }

        /// <summary>
        /// Unlocks player movement.
        /// </summary>
        public void UnlockMovement()
        {
            _movementLocked = false;
        }

        /// <summary>
        /// Returns true if the player is providing movement input.
        /// </summary>
        public bool HasMovementInput()
        {
            return _input != null && _input.Move.sqrMagnitude > 0.01f;
        }

        public void SetCameraRotateSensitivity(float value)
        {
            rotateSensitivity = value;
        }

        public void SetRotateOnMove(bool value)
        {
            rotateOnMove = value;
        }

        /// <summary>
        /// Sets the aiming state. When aiming, character always faces camera
        /// direction (strafe mode), sprint is disabled, and camera transitions to aim view.
        /// </summary>
        public void SetAiming(bool aiming)
        {
            if (_isAiming == aiming) return;
            _isAiming = aiming;

            // Delegate camera priority changes to PerspectiveSwitcher if present
            if (_perspectiveSwitcher != null)
            {
                _perspectiveSwitcher.SetAiming(aiming);
            }
        }

        /// <summary>
        /// Gets the camera pitch angle in degrees (negative = looking up, positive = looking down).
        /// Used by ShooterIKController for dynamic spine rotation.
        /// </summary>
        public float CameraPitch => _cinemachineTargetPitch;

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}