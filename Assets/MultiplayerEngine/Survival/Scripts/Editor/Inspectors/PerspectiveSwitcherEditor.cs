#if UNITY_EDITOR && (UNITY_SERVICES || STEAM_SERVICES)
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace Ignitives.MultiplayerEngine.Editor
{
    /// <summary>
    /// Premium custom editor for PerspectiveSwitcher leveraging the universal MEEditorInspector base.
    /// Provides categorized configurations, visual warning systems, and playmode diagnostics.
    /// </summary>
    [CustomEditor(typeof(PerspectiveSwitcher))]
    public class PerspectiveSwitcherEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Dual Perspective Camera Controller";

        // Default Perspective
        private SerializedProperty DefaultPerspective;

        // First Person
        private SerializedProperty FirstPersonCameraTarget;
        private SerializedProperty FirstPersonVirtualCamera;
        private SerializedProperty FirstPersonMesh;

        // Third Person
        private SerializedProperty ThirdPersonMesh;

        // Aim Camera
        private SerializedProperty AimVirtualCamera;

        // Camera Priorities
        private SerializedProperty activeCameraPriority;
        private SerializedProperty inactiveCameraPriority;

        // Custom Enable/Disable Lists
        private SerializedProperty EnableOnFirstPerson;
        private SerializedProperty DisableOnFirstPerson;

        private void OnEnable()
        {
            DefaultPerspective = serializedObject.FindProperty("DefaultPerspective");

            FirstPersonCameraTarget = serializedObject.FindProperty("FirstPersonCameraTarget");
            FirstPersonVirtualCamera = serializedObject.FindProperty("FirstPersonVirtualCamera");
            FirstPersonMesh = serializedObject.FindProperty("FirstPersonMesh");

            ThirdPersonMesh = serializedObject.FindProperty("ThirdPersonMesh");

            AimVirtualCamera = serializedObject.FindProperty("AimVirtualCamera");

            activeCameraPriority = serializedObject.FindProperty("activeCameraPriority");
            inactiveCameraPriority = serializedObject.FindProperty("inactiveCameraPriority");

            EnableOnFirstPerson = serializedObject.FindProperty("EnableOnFirstPerson");
            DisableOnFirstPerson = serializedObject.FindProperty("DisableOnFirstPerson");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Manages transitions between First Person (FP arms, target follow) and Third Person (full body mesh) perspectives with aim camera zoom controls.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Default Setup ──
            BeginCard("Default Perspective Setup");
            {
                DrawProperty(DefaultPerspective, "Default Camera Mode", "The camera perspective the player will default to upon spawn.");
            }
            EndCard();

            // ── Card 2: First Person Config ──
            BeginCard("First Person Camera Configuration");
            {
                DrawProperty(FirstPersonCameraTarget, "Head Follow Target", "Focus pivot placed on the character's head for camera viewport translation.");
                DrawProperty(FirstPersonVirtualCamera, "FP Virtual Camera", "Cinemachine virtual camera for First-Person views.");
                DrawProperty(FirstPersonMesh, "FP Arms Mesh", "Local-only rig representing the player's arms and held tools.");

                if (FirstPersonVirtualCamera.objectReferenceValue == null)
                {
                    DrawMessage("FP Virtual Camera is unassigned! First person mode will fail.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 3: Third Person Config ──
            BeginCard("Third Person Configuration");
            {
                DrawProperty(ThirdPersonMesh, "TP Body Mesh", "Rig representing the player's full body visible to others and in TP mode.");

                if (ThirdPersonMesh.objectReferenceValue == null)
                {
                    DrawMessage("Third Person Mesh is unassigned! The character will be invisible in TP mode.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 4: Aim Camera Config ──
            BeginCard("Aim Camera Configuration");
            {
                DrawProperty(AimVirtualCamera, "Aim Virtual Camera", "Over-the-shoulder cinemachine camera used when holding primary weapons or zooming.");

                if (AimVirtualCamera.objectReferenceValue == null)
                {
                    DrawMessage("Aim Virtual Camera is unassigned! Over-the-shoulder zoom will fall back to TP defaults.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 5: Priorities ──
            BeginCard("Cinemachine Camera Priorities");
            {
                DrawProperty(activeCameraPriority, "Active Camera Priority", "Priority coefficient applied to the current perspective camera.");
                DrawProperty(inactiveCameraPriority, "Inactive Camera Priority", "Priority coefficient applied to out-of-focus camera templates.");
            }
            EndCard();

            // ── Card 6: Custom Lists ──
            BeginCard("Custom Perspective Object Filters");
            {
                GUILayout.Label("<b>Enable on First Person View</b>", EditorStyles.miniBoldLabel);
                DrawMessage("These objects will automatically enable in First Person and disable in Third Person.", MessageType.None);
                DrawProperty(EnableOnFirstPerson, "First Person Enable List");
                
                GUILayout.Space(6);
                
                GUILayout.Label("<b>Disable on First Person View</b>", EditorStyles.miniBoldLabel);
                DrawMessage("These objects will automatically disable in First Person and enable in Third Person.", MessageType.None);
                DrawProperty(DisableOnFirstPerson, "First Person Disable List");
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
            BeginCard("Live Perspective Diagnostics");
            {
                var switcher = (PerspectiveSwitcher)target;

                // Basic states
                GUILayout.Label($"<b>Network Owner</b>: {(switcher.IsOwner ? "<color=#66CD00>LOCAL OWNER</color>" : "<color=#FFB90F>REMOTE CLIENT</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Active Perspective</b>: <color=#5CACEE>{switcher.CurrentPerspective}</color>", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Is First Person</b>: {(switcher.IsFirstPerson ? "<color=#66CD00>YES</color>" : "NO")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Is Strafe Mode Active</b>: {(switcher.IsStrafeMode ? "<color=#66CD00>YES</color>" : "NO")}", new GUIStyle(EditorStyles.label) { richText = true });

                // Reflection for aiming state
                var aimField = switcher.GetType().GetField("_isAiming", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isAiming = (bool)(aimField?.GetValue(switcher) ?? false);
                GUILayout.Label($"<b>Aim Mode Enabled</b>: {(isAiming ? "<color=#FFB90F>AIMING</color>" : "IDLE")}", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Admin commands
                GUILayout.Label("<b>Live Perspective Switcher Controls</b>", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Toggle Perspective", MEEditorTheme.StylePrimaryButton))
                    {
                        switcher.TogglePerspective();
                        Debug.Log($"[PerspectiveSwitcherEditor] Clicked Toggle Perspective. Current now: {switcher.CurrentPerspective}");
                    }

                    if (GUILayout.Button(isAiming ? "Disable Aiming" : "Enable Aiming", MEEditorTheme.StyleSecondaryButton))
                    {
                        switcher.SetAiming(!isAiming);
                        Debug.Log($"[PerspectiveSwitcherEditor] Toggled aiming state to: {!isAiming}");
                    }
                }
                GUILayout.EndHorizontal();

                Repaint();
            }
            EndCard();
        }
    }
}
#endif
