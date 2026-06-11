using UnityEngine;
using UnityEditor;
using System.Reflection;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Premium custom editor for InteractionController leveraging the universal MEEditorInspector base.
    /// Provides categorized configurations, visual warning systems, and playmode diagnostics.
    /// </summary>
    [CustomEditor(typeof(InteractionController))]
    public class InteractionControllerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "World Object Interactor & Ping Manager";

        // Properties
        private SerializedProperty maxInteractDistanceProp;
        private SerializedProperty pickupRadiusProp;
        private SerializedProperty hitLayersProp;
        private SerializedProperty detectLayersProp;
        private SerializedProperty interactionUIProp;
        private SerializedProperty debugModeProp;

        protected virtual void OnEnable()
        {
            maxInteractDistanceProp = serializedObject.FindProperty("maxInteractDistance");
            pickupRadiusProp = serializedObject.FindProperty("pickupRadius");
            hitLayersProp = serializedObject.FindProperty("hitLayers");
            detectLayersProp = serializedObject.FindProperty("detectLayers");
            interactionUIProp = serializedObject.FindProperty("interactionUI");
            debugModeProp = serializedObject.FindProperty("debugMode");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Manages screen-center raycast searches for pickables, doors, and triggers, routing RPCs and pings over Netcode.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Physics Bounds ──
            BeginCard("Interaction Bounds & Physics");
            {
                DrawProperty(maxInteractDistanceProp, "Max Interact Range", "Maximum screen-center raycast distance to reach interactables.");
                DrawProperty(pickupRadiusProp, "Overlap Sphere Radius", "Sphere overlap radius to assist focusing nearby interactable colliders.");

                if (maxInteractDistanceProp.floatValue <= 0)
                {
                    DrawMessage("Max Interact Distance must be greater than 0.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 2: Layers Config ──
            BeginCard("Collision & Layer Filtering");
            {
                DrawProperty(hitLayersProp, "Surface Hit Layers", "Layers the raycast detects to find the focus point (Ground, Walls).");
                DrawProperty(detectLayersProp, "Interactable Layers", "Layer masks allocated specifically to interactable objects.");

                if (hitLayersProp.intValue == 0 || detectLayersProp.intValue == 0)
                {
                    DrawMessage("Hit Layers or Detect Layers are completely unassigned! Object interaction will fail.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 3: HUD Interface ──
            BeginCard("Central HUD Interface");
            {
                DrawProperty(interactionUIProp, "Interaction UI Hud", "Reference to the canvas HUD overlay showcasing focused interactable prompts.");
                DrawProperty(debugModeProp, "Developer Debug Mode", "Draws center-aiming lines and overlap spheres in the editor scene.");

                if (interactionUIProp.objectReferenceValue == null)
                {
                    DrawMessage("Interaction UI HUD is unassigned! Prompts and item description tags will not display on screen.", MessageType.Warning);
                }
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
            BeginCard("Live Interaction Physics Monitor");
            {
                var controller = (InteractionController)target;

                // Basic Network status
                GUILayout.Label($"<b>Network Owner</b>: {(controller.IsOwner ? "<color=#66CD00>LOCAL OWNER</color>" : "<color=#FFB90F>REMOTE CLIENT</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                // Reflection search for currently focused object
                var focusField = controller.GetType().GetField("currentInteractable", BindingFlags.NonPublic | BindingFlags.Instance);
                var interactable = focusField?.GetValue(controller) as Interactable;

                if (interactable == null)
                {
                    GUILayout.Label("<b>Current Focus</b>: <color=#CD2626>None (Looking away)</color>", new GUIStyle(EditorStyles.label) { richText = true });
                }
                else
                {
                    GUILayout.Label($"<b>Current Focus</b>: <color=#66CD00>{interactable.DisplayName}</color>", new GUIStyle(EditorStyles.label) { richText = true });
                    GUILayout.Label($"<b>Type</b>: {interactable.InteractionType}  │  <b>Description</b>: {interactable.GetDescription()}", EditorStyles.miniLabel);
                    GUILayout.Label($"<b>Distance to Target</b>: {Vector3.Distance(controller.transform.position, interactable.transform.position):F2} meters", EditorStyles.miniLabel);

                    GUILayout.Space(10);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(10);

                    // Simulated interactive controls
                    GUILayout.Label("<b>Live Interaction Testing Controls</b>", EditorStyles.boldLabel);

                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Simulate Press 'E' (Interact)", MEEditorTheme.StylePrimaryButton))
                        {
                            var rpcMethod = controller.GetType().GetMethod("ServerInteractRpc", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (rpcMethod != null)
                            {
                                rpcMethod.Invoke(controller, new object[] { interactable.NetworkObjectId, default(RpcParams) });
                                Debug.Log($"[InteractionControllerEditor] Sent simulated interaction request for '{interactable.DisplayName}'.");
                            }
                            else
                            {
                                Debug.LogError("[InteractionControllerEditor] ServerInteractRpc method not found via reflection.");
                            }
                        }

                        if (GUILayout.Button("Simulate World Ping", MEEditorTheme.StyleSecondaryButton))
                        {
                            var netObj = interactable.GetComponent<NetworkObject>();
                            if (netObj == null)
                                netObj = interactable.GetComponentInParent<NetworkObject>();

                            if (netObj != null)
                            {
                                var pingMethod = controller.GetType().GetMethod("ServerPingObjectRpc", BindingFlags.NonPublic | BindingFlags.Instance);
                                pingMethod?.Invoke(controller, new object[] { netObj.NetworkObjectId, interactable.DisplayName });
                                Debug.Log($"[InteractionControllerEditor] Sent simulated Object Ping for '{interactable.DisplayName}'.");
                            }
                            else
                            {
                                var pingPosMethod = controller.GetType().GetMethod("ServerPingPositionRpc", BindingFlags.NonPublic | BindingFlags.Instance);
                                pingPosMethod?.Invoke(controller, new object[] { interactable.transform.position, interactable.DisplayName });
                                Debug.Log($"[InteractionControllerEditor] Sent simulated Position Ping at {interactable.transform.position}.");
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                Repaint();
            }
            EndCard();
        }
    }
}