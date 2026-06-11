using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ignitives.MultiplayerEngine
{
	public class InputManager : MonoBehaviour, IInputManager
	{
		[Header("Character Input Values")]
		[SerializeField] private Vector2 _move;
		[SerializeField] private Vector2 _look;
		[SerializeField] private bool _jump;
		[SerializeField] private bool _sprint;
		[SerializeField] private bool _interact;
		[SerializeField] private bool _aim;
		[SerializeField] private bool _action;

        public Vector2 Move => _move;
        public Vector2 Look => _look;
        public bool Jump { get => _jump; set => _jump = value; }
        public bool Sprint => _sprint;
        public bool Interact => _interact;
        public bool Aim => _aim;
        public bool Action => _action;

		[Header("Equipment Actions")]
		[SerializeField] private bool _primaryAction;       // Left mouse - attack/use
		[SerializeField] private bool _secondaryAction;     // Right mouse - block/aim
		[SerializeField] private bool _primaryActionDown;   // Frame when pressed
		[SerializeField] private bool _primaryActionUp;     // Frame when released
		[SerializeField] private bool _secondaryActionDown;
		[SerializeField] private bool _secondaryActionUp;

        public bool PrimaryAction => _primaryAction;
        public bool SecondaryAction => _secondaryAction;
        public bool PrimaryActionDown { get => _primaryActionDown; set => _primaryActionDown = value; }
        public bool PrimaryActionUp { get => _primaryActionUp; set => _primaryActionUp = value; }
        public bool SecondaryActionDown { get => _secondaryActionDown; set => _secondaryActionDown = value; }
        public bool SecondaryActionUp { get => _secondaryActionUp; set => _secondaryActionUp = value; }

		[Header("UI Actions")]
		[SerializeField] private bool _toggleInventory;     // Tab/I
		[SerializeField] private bool _toggleBuild;         // B
		[SerializeField] private bool _cancel;              // Escape
		[SerializeField] private float _scrollWheel;        // Mouse scroll
		[SerializeField] private bool _modifierHeld;        // Shift
		[SerializeField] private bool _rotateLeft;          // Q
		[SerializeField] private bool _rotateRight;         // E
		[SerializeField] private bool _deleteBuild;         // X - delete build piece
		[SerializeField] private bool _ping;                 // Z - ping interactable
		[SerializeField] private int _quickSlotPressed = -1;     // 1-8 keys (-1 = none)

        public bool ToggleInventory { get => _toggleInventory; set => _toggleInventory = value; }
        public bool ToggleBuild { get => _toggleBuild; set => _toggleBuild = value; }
        public bool Cancel { get => _cancel; set => _cancel = value; }
        public float ScrollWheel { get => _scrollWheel; set => _scrollWheel = value; }
        public bool ModifierHeld { get => _modifierHeld; set => _modifierHeld = value; }
        public bool RotateLeft { get => _rotateLeft; set => _rotateLeft = value; }
        public bool RotateRight { get => _rotateRight; set => _rotateRight = value; }
        public bool DeleteBuild { get => _deleteBuild; set => _deleteBuild = value; }
        public bool Ping { get => _ping; set => _ping = value; }
        public int QuickSlotPressed { get => _quickSlotPressed; set => _quickSlotPressed = value; }

		[Header("Camera")]
		[SerializeField] private bool _toggleCameraView;    // V key
        public bool ToggleCameraView { get => _toggleCameraView; set => _toggleCameraView = value; }

        [Header("Movement Settings")]
		[SerializeField] private bool _analogMovement;
        public bool AnalogMovement => _analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

        public bool CursorLocked => cursorLocked;
        public bool CursorInputForLook => cursorInputForLook;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnInteract(InputValue value)
		{
			// Only trigger on press, not hold - makes it a one-frame input
			if (value.isPressed) _interact = true;
		}
		public void OnAim(InputValue value)
		{
			_aim = value.isPressed;
		}
		public void OnAction(InputValue value)
		{
			_action = value.isPressed;
        }

		// Equipment Actions
		public void OnPrimaryAction(InputValue value)
		{
			bool pressed = value.isPressed;
			if (pressed && !_primaryAction) _primaryActionDown = true;
			if (!pressed && _primaryAction) _primaryActionUp = true;
			_primaryAction = pressed;
		}

		public void OnSecondaryAction(InputValue value)
		{
			bool pressed = value.isPressed;
			if (pressed && !_secondaryAction) _secondaryActionDown = true;
			if (!pressed && _secondaryAction) _secondaryActionUp = true;
			_secondaryAction = pressed;
		}

		// UI Actions
		public void OnToggleInventory(InputValue value)
		{
			if (value.isPressed) _toggleInventory = true;
		}

		public void OnToggleBuild(InputValue value)
		{
			if (value.isPressed) _toggleBuild = true;
		}

		public void OnCancel(InputValue value)
		{
			if (value.isPressed) _cancel = true;
		}

		public void OnScroll(InputValue value)
		{
			_scrollWheel = value.Get<float>();
		}

		public void OnModifier(InputValue value)
		{
			_modifierHeld = value.isPressed;
		}

		public void OnRotateLeft(InputValue value)
		{
			if (value.isPressed) _rotateLeft = true;
		}

		public void OnRotateRight(InputValue value)
		{
			if (value.isPressed) _rotateRight = true;
		}

		public void OnDeleteBuild(InputValue value)
		{
			if (value.isPressed) _deleteBuild = true;
		}

		public void OnPing(InputValue value)
		{
			if (value.isPressed) _ping = true;
		}

		public void OnQuickSlot1(InputValue value) { if (value.isPressed) _quickSlotPressed = 0; }
		public void OnQuickSlot2(InputValue value) { if (value.isPressed) _quickSlotPressed = 1; }
		public void OnQuickSlot3(InputValue value) { if (value.isPressed) _quickSlotPressed = 2; }
		public void OnQuickSlot4(InputValue value) { if (value.isPressed) _quickSlotPressed = 3; }
		public void OnQuickSlot5(InputValue value) { if (value.isPressed) _quickSlotPressed = 4; }
		public void OnQuickSlot6(InputValue value) { if (value.isPressed) _quickSlotPressed = 5; }
		public void OnQuickSlot7(InputValue value) { if (value.isPressed) _quickSlotPressed = 6; }
		public void OnQuickSlot8(InputValue value) { if (value.isPressed) _quickSlotPressed = 7; }

		public void OnToggleCameraView(InputValue value)
		{
			if (value.isPressed) _toggleCameraView = true;
		}
#endif

		private void Awake()
		{
			_quickSlotPressed = -1;
		}

		private void LateUpdate()
		{
			// Clear one-frame flags after all scripts have read them
			_primaryActionDown = false;
			_primaryActionUp = false;
			_secondaryActionDown = false;
			_secondaryActionUp = false;
			_toggleInventory = false;
			_toggleBuild = false;
			_cancel = false;
			_rotateLeft = false;
			_rotateRight = false;
			_quickSlotPressed = -1;
			_toggleCameraView = false;
			_interact = false;
			_deleteBuild = false;
			_ping = false;
		}

        public void MoveInput(Vector2 newMoveDirection)
		{
			_move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			_look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			_jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			_sprint = newSprintState;
		}

		public void InteractInput(bool newInteractState)
		{
			_interact = newInteractState;
        }

        public void AimInput(bool newAimState)
		{
			_aim = newAimState;
        }
		public void ActionInput(bool newActionState)
        {
			_action = newActionState;
        }

        private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}