using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Premium custom editor for PlayerController leveraging the universal MEEditorInspector base.
    /// Provides categorized configurations, visual warning systems, and playmode diagnostics.
    /// </summary>
    [CustomEditor(typeof(PlayerController))]
    public class PlayerControllerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Active Physics & Camera Controller";

        // Locomotive
        private SerializedProperty moveSpeedProp;
        private SerializedProperty sprintSpeedProp;
        private SerializedProperty rotationSmoothTimeProp;
        private SerializedProperty speedChangeRateProp;

        // Jump/Gravity
        private SerializedProperty jumpHeightProp;
        private SerializedProperty gravityProp;
        private SerializedProperty jumpTimeoutProp;
        private SerializedProperty fallTimeoutProp;

        // Grounded
        private SerializedProperty groundedOffsetProp;
        private SerializedProperty groundedRadiusProp;
        private SerializedProperty groundLayersProp;

        // Audio
        private SerializedProperty landingAudioClipProp;
        private SerializedProperty footstepAudioClipsProp;
        private SerializedProperty footstepAudioVolumeProp;

        // Cinemachine
        private SerializedProperty cinemachineCameraTargetProp;
        private SerializedProperty cinemachineVirtualCameraProp;
        private SerializedProperty topClampProp;
        private SerializedProperty bottomClampProp;
        private SerializedProperty cameraAngleOverrideProp;
        private SerializedProperty lockCameraPositionProp;

        protected virtual void OnEnable()
        {
            moveSpeedProp = serializedObject.FindProperty("MoveSpeed");
            sprintSpeedProp = serializedObject.FindProperty("SprintSpeed");
            rotationSmoothTimeProp = serializedObject.FindProperty("RotationSmoothTime");
            speedChangeRateProp = serializedObject.FindProperty("SpeedChangeRate");

            jumpHeightProp = serializedObject.FindProperty("JumpHeight");
            gravityProp = serializedObject.FindProperty("Gravity");
            jumpTimeoutProp = serializedObject.FindProperty("JumpTimeout");
            fallTimeoutProp = serializedObject.FindProperty("FallTimeout");

            groundedOffsetProp = serializedObject.FindProperty("GroundedOffset");
            groundedRadiusProp = serializedObject.FindProperty("GroundedRadius");
            groundLayersProp = serializedObject.FindProperty("GroundLayers");

            landingAudioClipProp = serializedObject.FindProperty("LandingAudioClip");
            footstepAudioClipsProp = serializedObject.FindProperty("FootstepAudioClips");
            footstepAudioVolumeProp = serializedObject.FindProperty("FootstepAudioVolume");

            cinemachineCameraTargetProp = serializedObject.FindProperty("CinemachineCameraTarget");
            cinemachineVirtualCameraProp = serializedObject.FindProperty("cinemachineVirtualCamera");
            topClampProp = serializedObject.FindProperty("TopClamp");
            bottomClampProp = serializedObject.FindProperty("BottomClamp");
            cameraAngleOverrideProp = serializedObject.FindProperty("CameraAngleOverride");
            lockCameraPositionProp = serializedObject.FindProperty("LockCameraPosition");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Manages locomotions, camera orbiting pitch/yaw limits, falling physics, and animator feedback blends.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Locomotion Speeds ──
            BeginCard("Movement Settings");
            {
                DrawProperty(moveSpeedProp, "Walk Speed", "Forward/Strafe walking speed in meters per second.");
                DrawProperty(sprintSpeedProp, "Sprint Speed", "Maximum sprinting speed in meters per second.");
                DrawProperty(rotationSmoothTimeProp, "Rotation Smooth Time", "Damping time constant to face camera movement directions.");
                DrawProperty(speedChangeRateProp, "Speed Change Rate", "Acceleration and deceleration blending coefficient.");
            }
            EndCard();

            // ── Card 2: Jump & Gravity ──
            BeginCard("Physics, Jump & Gravity");
            {
                DrawProperty(jumpHeightProp, "Jump Target Height", "Velocity-calculated peak height player can jump.");
                DrawProperty(gravityProp, "Gravity Constant", "Gravity coefficient. Unity engine standard is -9.81m/s2.");
                DrawProperty(jumpTimeoutProp, "Jump Cooldown", "Required cooldown delay before jumps can trigger again.");
                DrawProperty(fallTimeoutProp, "Fall Timeout", "Grace delay before entering a freefall animation state.");
            }
            EndCard();

            // ── Card 3: Grounded Constraints ──
            BeginCard("Grounded Checks");
            {
                DrawProperty(groundedOffsetProp, "Grounded Offset Y", "Vertical offset relative to pivot where grounded sphere check is placed.");
                DrawProperty(groundedRadiusProp, "Sphere Check Radius", "Radius of the contact check sphere (should match character controller radius).");
                DrawProperty(groundLayersProp, "Grounded Layers", "Layer masks representing stable ground (Terrain, Walls, Platforms).");
            }
            EndCard();

            // ── Card 4: Cinemachine Target Clamps ──
            BeginCard("Orbit Camera Settings");
            {
                DrawProperty(cinemachineCameraTargetProp, "Camera Focus Target", "Game object child pivot which the cinemachine camera orbits.");
                DrawProperty(cinemachineVirtualCameraProp, "Virtual Camera Object", "Cinemachine camera rendering this player.");
                
                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                GUILayout.Label("<b>Clamps & Angle Overrides</b>", EditorStyles.miniBoldLabel);
                DrawProperty(topClampProp, "Camera Pitch Top Clamp", "Maximum looking upward degree angle clamp.");
                DrawProperty(bottomClampProp, "Camera Pitch Bottom Clamp", "Maximum looking downward degree angle clamp.");
                DrawProperty(cameraAngleOverrideProp, "Camera Yaw Override", "Fine tuning offset applied to yaw alignments.");
                DrawProperty(lockCameraPositionProp, "Lock Camera position", "Completely freezes camera orbiting.");

                if (cinemachineCameraTargetProp.objectReferenceValue == null)
                {
                    DrawMessage("Cinemachine camera target is unassigned! Look orbits will fail.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 5: Audio Feedbacks ──
            BeginCard("Locomotive Audio FX");
            {
                DrawProperty(landingAudioClipProp, "Landing Impact Clip", "SFX played on structural landing collisions.");
                DrawProperty(footstepAudioClipsProp, "Footstep Sound Clips", "List of audios cycled on footstep animation events.");
                DrawProperty(footstepAudioVolumeProp, "SFX Volume", "Audio scaling volume slider.");
            }
            EndCard();

            // ── Playmode Live Debugger ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Player Physics Monitor");
            {
                var controller = (PlayerController)target;

                // Basic Network states
                GUILayout.Label($"<b>Network Ownership</b>: {(controller.IsOwner ? "<color=#66CD00>LOCAL PLAYER (OWNER)</color>" : "<color=#FFB90F>REMOTE CLIENT</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Is Grounded</b>: {(controller.Grounded ? "<color=#66CD00>GROUNDED</color>" : "<color=#CD2626>AIRBORNE / FALLING</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                // Reflection search for internal values
                float speed = (float)(controller.GetType().GetField("_speed", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? 0f);
                GUILayout.Label($"<b>Current velocity</b>: {speed:F2} m/s", EditorStyles.miniLabel);

                bool isAiming = (bool)(controller.GetType().GetField("_isAiming", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Aiming State (Strafe Mode)</b>: {(isAiming ? "<color=#5CACEE>ACTIVE</color>" : "INACTIVE")}", new GUIStyle(EditorStyles.miniLabel) { richText = true });

                bool locked = (bool)(controller.GetType().GetField("_movementLocked", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Movement Lock</b>: {(locked ? "<color=#CD2626>LOCKED</color>" : "UNLOCKED")}", new GUIStyle(EditorStyles.miniLabel) { richText = true });

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Admin simulation commands
                GUILayout.Label("<b>Live Movement Testing Controls</b>", EditorStyles.boldLabel);
                
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Simulate Jump Force", MEEditorTheme.StylePrimaryButton))
                    {
                        var velField = controller.GetType().GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (velField != null)
                        {
                            velField.SetValue(controller, Mathf.Sqrt(controller.JumpHeight * -2f * controller.Gravity));
                            Debug.Log("[PlayerControllerEditor] Injected upward vertical velocity jump force.");
                        }
                    }

                    if (locked)
                    {
                        if (GUILayout.Button("Unlock Locomotions", MEEditorTheme.StyleSecondaryButton))
                        {
                            controller.UnlockMovement();
                            Debug.Log("[PlayerControllerEditor] Movement unlocked.");
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Lock Locomotions", MEEditorTheme.StyleSecondaryButton))
                        {
                            controller.LockMovement();
                            Debug.Log("[PlayerControllerEditor] Movement locked.");
                        }
                    }
                }
                GUILayout.EndHorizontal();

                Repaint();
            }
            EndCard();
        }
    }
}