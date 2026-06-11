using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for InputManager to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes real-time input axis and action monitoring in playmode.
    /// </summary>
    [CustomEditor(typeof(InputManager))]
    public class InputManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Unified Input System Listener";

        // Serialized properties
        private SerializedProperty moveProp;
        private SerializedProperty lookProp;
        private SerializedProperty jumpProp;
        private SerializedProperty sprintProp;
        private SerializedProperty interactProp;
        private SerializedProperty aimProp;
        private SerializedProperty actionProp;

        private SerializedProperty primaryActionProp;
        private SerializedProperty secondaryActionProp;
        private SerializedProperty primaryActionDownProp;
        private SerializedProperty primaryActionUpProp;
        private SerializedProperty secondaryActionDownProp;
        private SerializedProperty secondaryActionUpProp;

        private SerializedProperty toggleInventoryProp;
        private SerializedProperty toggleBuildProp;
        private SerializedProperty cancelProp;
        private SerializedProperty scrollWheelProp;
        private SerializedProperty modifierHeldProp;
        private SerializedProperty rotateLeftProp;
        private SerializedProperty rotateRightProp;
        private SerializedProperty deleteBuildProp;
        private SerializedProperty pingProp;
        private SerializedProperty quickSlotPressedProp;

        private SerializedProperty toggleCameraViewProp;
        private SerializedProperty analogMovementProp;
        private SerializedProperty cursorLockedProp;
        private SerializedProperty cursorInputForLookProp;

        private void OnEnable()
        {
            moveProp = serializedObject.FindProperty("_move");
            lookProp = serializedObject.FindProperty("_look");
            jumpProp = serializedObject.FindProperty("_jump");
            sprintProp = serializedObject.FindProperty("_sprint");
            interactProp = serializedObject.FindProperty("_interact");
            aimProp = serializedObject.FindProperty("_aim");
            actionProp = serializedObject.FindProperty("_action");

            primaryActionProp = serializedObject.FindProperty("_primaryAction");
            secondaryActionProp = serializedObject.FindProperty("_secondaryAction");
            primaryActionDownProp = serializedObject.FindProperty("_primaryActionDown");
            primaryActionUpProp = serializedObject.FindProperty("_primaryActionUp");
            secondaryActionDownProp = serializedObject.FindProperty("_secondaryActionDown");
            secondaryActionUpProp = serializedObject.FindProperty("_secondaryActionUp");

            toggleInventoryProp = serializedObject.FindProperty("_toggleInventory");
            toggleBuildProp = serializedObject.FindProperty("_toggleBuild");
            cancelProp = serializedObject.FindProperty("_cancel");
            scrollWheelProp = serializedObject.FindProperty("_scrollWheel");
            modifierHeldProp = serializedObject.FindProperty("_modifierHeld");
            rotateLeftProp = serializedObject.FindProperty("_rotateLeft");
            rotateRightProp = serializedObject.FindProperty("_rotateRight");
            deleteBuildProp = serializedObject.FindProperty("_deleteBuild");
            pingProp = serializedObject.FindProperty("_ping");
            quickSlotPressedProp = serializedObject.FindProperty("_quickSlotPressed");

            toggleCameraViewProp = serializedObject.FindProperty("_toggleCameraView");
            analogMovementProp = serializedObject.FindProperty("_analogMovement");
            cursorLockedProp = serializedObject.FindProperty("cursorLocked");
            cursorInputForLookProp = serializedObject.FindProperty("cursorInputForLook");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Captures inputs from Unity's Input System and routes them to movement, actions, and HUD elements.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Cursor & Base Settings ──
            BeginCard("Cursor & Aiming Settings");
            {
                DrawProperty(cursorLockedProp, "Lock Cursor", "Determines if the mouse cursor is bound and locked to the center of the game view.");
                DrawProperty(cursorInputForLookProp, "Cursor Look Input", "Routes delta mouse cursor movements into player camera looking angles.");
                DrawProperty(analogMovementProp, "Analog Movement", "Enables smoothly scaled vector movements from controller thumbsticks.");
            }
            EndCard();

            // ── Card 2: Character Input Vectors & Actions ──
            BeginCard("Character Action Settings");
            {
                DrawProperty(moveProp, "Character Movement", "Current Vector2 movement direction input.");
                DrawProperty(lookProp, "Camera Look Delta", "Current Vector2 camera look delta input.");
                DrawProperty(jumpProp, "Jump Button", "Jump action trigger state.");
                DrawProperty(sprintProp, "Sprint Button", "Sprint action toggle state.");
                DrawProperty(interactProp, "Interact Button", "Interact action trigger state.");
                DrawProperty(aimProp, "Aim Button", "Aim down sight action state.");
                DrawProperty(actionProp, "Action Button", "General action trigger state.");
            }
            EndCard();

            // ── Card 3: Equipment Inputs ──
            BeginCard("Equipment & Tool Actions");
            {
                DrawProperty(primaryActionProp, "Primary Action (Attack)", "Hold state for attack/use button (Left Click).");
                DrawProperty(secondaryActionProp, "Secondary Action (Block)", "Hold state for block/aim button (Right Click).");
                
                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(4);
                
                GUILayout.Label("<b>Instant Button Presses</b>", EditorStyles.miniBoldLabel);
                DrawProperty(primaryActionDownProp, "Primary Down", "Frame-perfect trigger when Primary Action is pressed down.");
                DrawProperty(primaryActionUpProp, "Primary Up", "Frame-perfect trigger when Primary Action is released.");
                DrawProperty(secondaryActionDownProp, "Secondary Down", "Frame-perfect trigger when Secondary Action is pressed down.");
                DrawProperty(secondaryActionUpProp, "Secondary Up", "Frame-perfect trigger when Secondary Action is released.");
            }
            EndCard();

            // ── Card 4: UI & Hotbar Actions ──
            BeginCard("UI & Building Controls");
            {
                DrawProperty(toggleInventoryProp, "Toggle Inventory", "Open/close full inventory UI panel.");
                DrawProperty(toggleBuildProp, "Toggle Build Mode", "Open/close modular building system.");
                DrawProperty(cancelProp, "Cancel / Escape", "Cancel active building ghost or exit panels.");
                DrawProperty(scrollWheelProp, "Scroll Wheel Delta", "Delta scroll amount (for cycling hotbars).");
                DrawProperty(modifierHeldProp, "Modifier Held", "Modifier key held state (e.g. Shift / Control).");
                
                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(4);

                GUILayout.Label("<b>Building & Navigation</b>", EditorStyles.miniBoldLabel);
                DrawProperty(rotateLeftProp, "Rotate Left", "Rotate placed ghost counter-clockwise.");
                DrawProperty(rotateRightProp, "Rotate Right", "Rotate placed ghost clockwise.");
                DrawProperty(deleteBuildProp, "Delete Build Piece", "Demolish focused modular build piece.");
                DrawProperty(pingProp, "Ping Location", "Ping focused point of interest overlay.");
                DrawProperty(quickSlotPressedProp, "Quick-Slot Binds", "Cycles between slot indices 0-7 pressed (-1 for none).");
            }
            EndCard();

            // ── Card 5: Camera Controls ──
            BeginCard("Camera Perspective");
            {
                DrawProperty(toggleCameraViewProp, "Toggle View Mode", "Swaps perspective between first-person and third-person.");
            }
            EndCard();

            // ── Playmode Live Debugger Badges ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Input System Binds Tracker");
            {
                var manager = (InputManager)target;

                GUILayout.Label($"<b>Locked State</b>: {(manager.CursorLocked ? "<color=#66CD00>LOCKED</color>" : "<color=#CD2626>FREE</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Look Input State</b>: {(manager.CursorInputForLook ? "<color=#66CD00>ENABLED</color>" : "<color=#CD2626>DISABLED</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                GUILayout.Label("<b>Active Movement Vectors</b>", EditorStyles.boldLabel);
                GUILayout.Label($"<b>Move Vector (WASD/L-Stick)</b>: {manager.Move}", EditorStyles.miniLabel);
                GUILayout.Label($"<b>Look Delta (Mouse/R-Stick)</b>: {manager.Look}", EditorStyles.miniLabel);

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                GUILayout.Label("<b>Active Action Triggers</b>", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                {
                    DrawInputBadge("Jump", manager.Jump);
                    DrawInputBadge("Sprint", manager.Sprint);
                    DrawInputBadge("Interact", manager.Interact);
                    DrawInputBadge("Aim", manager.Aim);
                    DrawInputBadge("Action", manager.Action);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                {
                    DrawInputBadge("Primary Action", manager.PrimaryAction);
                    DrawInputBadge("Secondary Action", manager.SecondaryAction);
                    DrawInputBadge("Delete Build", manager.DeleteBuild);
                    DrawInputBadge("Ping POI", manager.Ping);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                GUILayout.Label("<b>Active Interface Actions</b>", EditorStyles.boldLabel);
                GUILayout.Label($"<b>Scroll Wheel Delta</b>: {manager.ScrollWheel:F2}", EditorStyles.miniLabel);
                GUILayout.Label($"<b>Modifier held (Shift/Alt)</b>: {(manager.ModifierHeld ? "<color=#5CACEE>YES</color>" : "NO")}", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                
                int slot = manager.QuickSlotPressed;
                GUILayout.Label($"<b>Quick-Slot Hotkey Pressed</b>: {(slot >= 0 ? $"<color=#66CD00>SLOT {slot + 1}</color>" : "<i>None</i>")}", new GUIStyle(EditorStyles.miniLabel) { richText = true });

                Repaint();
            }
            EndCard();
        }

        private void DrawInputBadge(string label, bool active)
        {
            Color baseColor = active ? new Color(0.2f, 0.6f, 0.2f, 1f) : new Color(0.15f, 0.15f, 0.15f, 0.2f);
            Color outlineColor = active ? Color.green : new Color(0.3f, 0.3f, 0.3f, 0.3f);
            
            GUILayout.BeginVertical(GUILayout.Width(75));
            {
                Rect rect = GUILayoutUtility.GetRect(75, 18);
                Handles.DrawSolidRectangleWithOutline(rect, baseColor, outlineColor);
                
                GUIStyle textStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                textStyle.normal.textColor = active ? Color.white : Color.gray;
                
                GUI.Label(rect, label, textStyle);
            }
            GUILayout.EndVertical();
        }
    }
}
