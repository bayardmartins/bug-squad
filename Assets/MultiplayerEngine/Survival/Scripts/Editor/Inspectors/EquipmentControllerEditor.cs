using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Premium custom editor for EquipmentController leveraging the universal MEEditorInspector base.
    /// Provides categorized configurations, visual warning systems, and playmode diagnostics.
    /// </summary>
    [CustomEditor(typeof(EquipmentController))]
    public class EquipmentControllerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Active Equipment & Rigging Controller";

        // Properties
        private SerializedProperty rightHandHoldPointProp;
        private SerializedProperty leftHandHoldPointProp;
        private SerializedProperty playerAnimatorProp;
        private SerializedProperty upperBodyLayerIndexProp;
        private SerializedProperty layerBlendSpeedProp;
        private SerializedProperty inputManagerProp;
        private SerializedProperty characterIKDataProp;

        // For test features in playmode
        private int testSlotIndex = 0;

        protected virtual void OnEnable()
        {
            rightHandHoldPointProp = serializedObject.FindProperty("rightHandHoldPoint");
            leftHandHoldPointProp = serializedObject.FindProperty("leftHandHoldPoint");
            playerAnimatorProp = serializedObject.FindProperty("playerAnimator");
            upperBodyLayerIndexProp = serializedObject.FindProperty("upperBodyLayerIndex");
            layerBlendSpeedProp = serializedObject.FindProperty("layerBlendSpeed");
            inputManagerProp = serializedObject.FindProperty("inputManager");
            characterIKDataProp = serializedObject.FindProperty("characterIKData");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Manages equipped items, input routing, holster transforms, and custom rigging/IK setups.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Hand Hold Points ──
            BeginCard("Rigging & Hold Points");
            {
                DrawProperty(rightHandHoldPointProp, "Right Hand Anchor", "Transform reference on character skeleton for holding main tools/weapons.");
                DrawProperty(leftHandHoldPointProp, "Left Hand Anchor", "Transform reference on character skeleton for secondary/dual-wield items.");

                if (rightHandHoldPointProp.objectReferenceValue == null)
                {
                    DrawMessage("Right Hand Hold Point is unassigned! Item models will not be able to mount properly.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 2: Upper Body Blending ──
            BeginCard("Upper Body Animation Settings");
            {
                DrawProperty(playerAnimatorProp, "Player Animator", "Animator controller containing upper body rigging layers.");
                DrawProperty(upperBodyLayerIndexProp, "Default Rig Layer", "Index of Upper Body layer (usually 1).");
                DrawProperty(layerBlendSpeedProp, "Blend Transition Speed", "Speed at which weight transitions occur (default is 8).");

                if (playerAnimatorProp.objectReferenceValue == null)
                {
                    DrawMessage("Animator is not assigned! Character animations and upper body weight blending will fail.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 3: Interfaces & IK Sets ──
            BeginCard("System References & IK Overrides");
            {
                DrawProperty(inputManagerProp, "Input Manager Component", "Routes primary/secondary click bindings to active item handlers.");
                DrawProperty(characterIKDataProp, "Character IK Data", "Optional asset overriding item-specific hand alignment parameters.");
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
            BeginCard("Live Equipment Physics Monitor");
            {
                var controller = (EquipmentController)target;

                // Basic states
                GUILayout.Label($"<b>Network Owner</b>: {(controller.IsOwner ? "<color=#66CD00>LOCAL OWNER</color>" : "<color=#FFB90F>REMOTE CLIENT</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Equipped Item ID</b>: {(controller.EquippedItemId >= 0 ? $"<color=#66CD00>{controller.EquippedItemId}</color>" : "<i>None</i>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Active Slot Index</b>: {(controller.CurrentSlotIndex >= 0 ? $"<color=#66CD00>{controller.CurrentSlotIndex}</color>" : "<i>None</i>")}", new GUIStyle(EditorStyles.label) { richText = true });

                // Reflection for internal states
                var handlerField = controller.GetType().GetField("currentHandler", BindingFlags.NonPublic | BindingFlags.Instance);
                var handlerValue = handlerField?.GetValue(controller);
                string handlerName = handlerValue != null ? handlerValue.GetType().Name : "None";
                GUILayout.Label($"<b>Active Handler</b>: <color=#5CACEE>{handlerName}</color>", new GUIStyle(EditorStyles.label) { richText = true });

                var swapPlayingField = controller.GetType().GetField("isSwapAnimationPlaying", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isSwapPlaying = (bool)(swapPlayingField?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Swap Anim Playing</b>: {(isSwapPlaying ? "<color=#FFB90F>TRUE</color>" : "FALSE")}", new GUIStyle(EditorStyles.label) { richText = true });

                var pendingSwapField = controller.GetType().GetField("hasPendingSwap", BindingFlags.NonPublic | BindingFlags.Instance);
                bool hasPendingSwap = (bool)(pendingSwapField?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Pending Swap</b>: {(hasPendingSwap ? "<color=#FFB90F>TRUE</color>" : "FALSE")}", new GUIStyle(EditorStyles.label) { richText = true });

                var actionField = controller.GetType().GetField("isInAction", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isInAction = (bool)(actionField?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Is In Action</b>: {(isInAction ? "<color=#CD2626>TRUE</color>" : "FALSE")}", new GUIStyle(EditorStyles.label) { richText = true });

                var comboField = controller.GetType().GetField("isInComboAttack", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isInCombo = (bool)(comboField?.GetValue(controller) ?? false);
                GUILayout.Label($"<b>Combo Attack Active</b>: {(isInCombo ? "<color=#66CD00>TRUE</color>" : "FALSE")}", new GUIStyle(EditorStyles.label) { richText = true });

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Admin simulation commands
                GUILayout.Label("<b>Live Rig Testing Controls</b>", EditorStyles.boldLabel);

                testSlotIndex = EditorGUILayout.IntField("Simulated Slot Index", testSlotIndex);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Simulate Slot Swap", MEEditorTheme.StylePrimaryButton))
                    {
                        var swapMethod = controller.GetType().GetMethod("RequestEquipFromSlot", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (swapMethod != null)
                        {
                            swapMethod.Invoke(controller, new object[] { testSlotIndex });
                            Debug.Log($"[EquipmentControllerEditor] Requesting simulated swap to slot index {testSlotIndex}.");
                        }
                        else
                        {
                            Debug.LogError("[EquipmentControllerEditor] RequestEquipFromSlot method not found via reflection.");
                        }
                    }

                    if (GUILayout.Button("Silent Unequip", MEEditorTheme.StyleSecondaryButton))
                    {
                        controller.UnequipSilent();
                        Debug.Log("[EquipmentControllerEditor] Triggered silent unequip.");
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Simulate Swap Complete", MEEditorTheme.StyleSecondaryButton))
                    {
                        controller.OnSwapComplete();
                        Debug.Log("[EquipmentControllerEditor] Simulated OnSwapComplete animation event.");
                    }

                    if (GUILayout.Button("Simulate Action End", MEEditorTheme.StyleSecondaryButton))
                    {
                        controller.OnActionEnded();
                        Debug.Log("[EquipmentControllerEditor] Simulated OnActionEnded callback.");
                    }
                }
                GUILayout.EndHorizontal();

                Repaint();
            }
            EndCard();
        }
    }
}