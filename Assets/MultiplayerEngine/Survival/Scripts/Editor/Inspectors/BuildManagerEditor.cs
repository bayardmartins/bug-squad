using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.Reflection;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for BuildManager to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes a playmode debugger, reflective administration controls, and stability tools.
    /// </summary>
    [CustomEditor(typeof(BuildManager))]
    public class BuildManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Unified Modular Build & Sync System";

        // Serialized properties
        private SerializedProperty groundLayerProp;
        private SerializedProperty buildPieceLayerProp;
        private SerializedProperty ignoreLayersProp;
        private SerializedProperty validMaterialProp;
        private SerializedProperty invalidMaterialProp;
        private SerializedProperty blueprintMaterialProp;
        private SerializedProperty outlineMaterialProp;
        private SerializedProperty placementModeProp;
        private SerializedProperty maxSnapDistanceProp;
        private SerializedProperty maxBuildDistanceProp;
        private SerializedProperty sphereCastRadiusProp;
        private SerializedProperty collapseThresholdProp;
        private SerializedProperty cascadeDelayProp;
        private SerializedProperty buildMenuUIProp;
        private SerializedProperty buildDatabaseProp;
        private SerializedProperty buildPieceLayerNameProp;
        private SerializedProperty autoSaveDelayProp;

        private void OnEnable()
        {
            groundLayerProp = serializedObject.FindProperty("groundLayer");
            buildPieceLayerProp = serializedObject.FindProperty("buildPieceLayer");
            ignoreLayersProp = serializedObject.FindProperty("ignoreLayers");
            validMaterialProp = serializedObject.FindProperty("validMaterial");
            invalidMaterialProp = serializedObject.FindProperty("invalidMaterial");
            blueprintMaterialProp = serializedObject.FindProperty("blueprintMaterial");
            outlineMaterialProp = serializedObject.FindProperty("outlineMaterial");
            placementModeProp = serializedObject.FindProperty("placementMode");
            maxSnapDistanceProp = serializedObject.FindProperty("maxSnapDistance");
            maxBuildDistanceProp = serializedObject.FindProperty("maxBuildDistance");
            sphereCastRadiusProp = serializedObject.FindProperty("sphereCastRadius");
            collapseThresholdProp = serializedObject.FindProperty("collapseThreshold");
            cascadeDelayProp = serializedObject.FindProperty("cascadeDelay");
            buildMenuUIProp = serializedObject.FindProperty("buildMenuUI");
            buildDatabaseProp = serializedObject.FindProperty("buildDatabase");
            buildPieceLayerNameProp = serializedObject.FindProperty("buildPieceLayerName");
            autoSaveDelayProp = serializedObject.FindProperty("autoSaveDelay");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Central coordinator for ghost placement, structural stability validation, and modular building sync.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Aiming & Collision Layers ──
            BeginCard("Aiming & Layer Settings");
            {
                DrawProperty(groundLayerProp, "Terrain/Ground Layer", "Layer representing steady support surfaces (terrain, rocks, foundations).");
                DrawProperty(buildPieceLayerProp, "Build Piece Layer", "Layer containing spawned modular wall/floor build pieces.");
                DrawProperty(ignoreLayersProp, "Ignore Raycast Layers", "Layer masks ignored during placement collision tests.");
                DrawProperty(buildPieceLayerNameProp, "Layer Name string", "Label of the layer applied to newly spawned pieces.");

                if (groundLayerProp.intValue == 0 || buildPieceLayerProp.intValue == 0)
                {
                    DrawMessage("Ground or Build Piece layers are unassigned! Aiming and placement raycasts may fail.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 2: Hologram Materials ──
            BeginCard("Ghost Preview Materials");
            {
                DrawProperty(validMaterialProp, "Valid Placement", "Green/blue semi-transparent preview material.");
                DrawProperty(invalidMaterialProp, "Blocked/Invalid Placement", "Red semi-transparent preview material.");
                DrawProperty(blueprintMaterialProp, "Blueprint Material", "Holographic preview used for blueprint placeholders.");
                DrawProperty(outlineMaterialProp, "Hover Outline Material", "High-contrast outline shader for demolish focus.");
            }
            EndCard();

            // ── Card 3: Construction & Constraints ──
            BeginCard("Build Mode Settings");
            {
                DrawProperty(placementModeProp, "Hologram mode", "Direct Place (consume instantly) vs. Blueprint (place outline first).");
                DrawProperty(maxSnapDistanceProp, "Max Snap Distance", "Maximum range to snap preview pieces to adjacent sockets.");
                DrawProperty(maxBuildDistanceProp, "Max Aim Distance", "Maximum raycast length to position ghosts.");
                DrawProperty(sphereCastRadiusProp, "Crosshair Radius", "Width of spherical raycast aiming (0 = pixel perfect, 0.15f = lenient).");
            }
            EndCard();

            // ── Card 4: Structural Support Graph ──
            BeginCard("Physics & Graph Stability");
            {
                DrawProperty(collapseThresholdProp, "Collapse stability", "Minimum stability level (0 to 1). Pieces dropping below this collapse.");
                DrawProperty(cascadeDelayProp, "Cascade Delay (Secs)", "Wave separation timing between cascading physical collapses.");

                DrawMessage("Stability is calculated dynamically via Dijkstra. Pieces are supported by Ground-touched foundations.", MessageType.Info);
            }
            EndCard();

            // ── Card 5: Core Services & Database ──
            BeginCard("Database & Saves");
            {
                DrawProperty(buildDatabaseProp, "Build Database Asset", "Registry asset mapping pieces to unique network prefab indices.");
                DrawProperty(buildMenuUIProp, "Build Menu HUD UI", "HUD canvas panel used to select active build targets.");
                DrawProperty(autoSaveDelayProp, "Auto-Save Delay (Secs)", "Server cooldown delay to save structural changes to disk.");

                if (buildDatabaseProp.objectReferenceValue == null)
                {
                    DrawMessage("Build Database is unassigned! Networking structures will crash without registration index lookups.", MessageType.Error);
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
            BeginCard("Live Build System Monitor");
            {
                var manager = (BuildManager)target;

                // Basic states
                GUILayout.Label($"<b>Active Mode</b>: {(manager.IsBuildModeActive ? "<color=#66CD00>BUILDING ACTIVE</color>" : "<color=#5CACEE>IDLE</color>")}", new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.Label($"<b>Network Authority</b>: {(manager.IsServer ? "<color=#66CD00>SERVER AUTHORITATIVE</color>" : "<color=#FFB90F>CLIENT ONLY</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                // Reflection for count
                var listField = manager.GetType().GetField("buildPieces", BindingFlags.NonPublic | BindingFlags.Instance);
                var piecesList = listField?.GetValue(manager) as NetworkList<BuildPieceData>;
                int piecesCount = piecesList != null ? piecesList.Count : 0;

                GUILayout.Label($"<b>Total Placed Pieces</b>: <color=#66CD00>{piecesCount}</color> items", new GUIStyle(EditorStyles.label) { richText = true });

                // Hovered details
                var hovered = manager.HoveredPiece;
                if (hovered != null)
                {
                    GUILayout.Space(4);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(4);
                    GUILayout.Label($"<b>Focused piece ID</b>: {hovered.pieceId} (Prefab Index: {manager.GetPrefabIndexForPiece(hovered.pieceId)})", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                    GUILayout.Label($"<b>Stability rating</b>: {hovered.GhostStability * 100f:F1}%", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Admin triggers (Server Only triggers)
                GUILayout.Label("<b>Server Administration Operations</b>", EditorStyles.boldLabel);
                
                GUILayout.BeginHorizontal();
                {
                    var saveMethod = manager.GetType().GetMethod("PerformSave", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (GUILayout.Button("Force Auto-Save", MEEditorTheme.StylePrimaryButton))
                    {
                        saveMethod?.Invoke(manager, null);
                        Debug.Log("[BuildManagerEditor] Triggered building save cycle on server.");
                    }

                    var reconstructMethod = manager.GetType().GetMethod("ReconstructAllConnectionsAndSupport", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (GUILayout.Button("Reconstruct Graph", MEEditorTheme.StyleSecondaryButton))
                    {
                        reconstructMethod?.Invoke(manager, null);
                        Debug.Log("[BuildManagerEditor] Reconstructed all structural stability graph linkages.");
                    }
                }
                GUILayout.EndHorizontal();

                if (manager.IsServer && piecesList != null && piecesCount > 0)
                {
                    GUILayout.Space(4);
                    Color oldCol = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f);
                    if (GUILayout.Button("Demolish All Buildings (Wipe Server)", MEEditorTheme.StylePrimaryButton))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Server Wipe", "Are you sure you want to demolish ALL placed buildings on the server?", "Wipe Everything", "Cancel"))
                        {
                            piecesList.Clear();
                            Debug.Log("[BuildManagerEditor] Administrative server wipe completed successfully.");
                        }
                    }
                    GUI.backgroundColor = oldCol;
                }

                Repaint();
            }
            EndCard();
        }
    }
}
