using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for reading player input.
    /// Abstracts input so AI or other controllers can feed input to the player controller.
    /// </summary>
    public interface IInputManager
    {
        // Character Input
        bool enabled { get; set; }
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool Jump { get; set; }
        bool Sprint { get; }
        bool Interact { get; }
        bool Aim { get; }
        bool Action { get; }

        // Equipment Actions
        bool PrimaryAction { get; }
        bool SecondaryAction { get; }
        bool PrimaryActionDown { get; }
        bool PrimaryActionUp { get; }
        bool SecondaryActionDown { get; }
        bool SecondaryActionUp { get; }

        // UI Actions
        bool ToggleInventory { get; }
        bool ToggleBuild { get; }
        bool Cancel { get; }
        float ScrollWheel { get; }
        bool ModifierHeld { get; }
        bool RotateLeft { get; }
        bool RotateRight { get; }
        bool DeleteBuild { get; }
        bool Ping { get; }
        int QuickSlotPressed { get; }

        // Camera
        bool ToggleCameraView { get; }

        // Settings
        bool AnalogMovement { get; }
        bool CursorLocked { get; }
        bool CursorInputForLook { get; }
        
        // Input injection methods (useful for AI or mobile virtual controls)
        void MoveInput(Vector2 newMoveDirection);
        void LookInput(Vector2 newLookDirection);
        void JumpInput(bool newJumpState);
        void SprintInput(bool newSprintState);
        void InteractInput(bool newInteractState);
        void AimInput(bool newAimState);
        void ActionInput(bool newActionState);
    }
}
