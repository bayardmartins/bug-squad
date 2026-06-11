using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for MarkerManager to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Features deep reflection debugging in Play Mode.
    /// </summary>
    [CustomEditor(typeof(MarkerManager))]
    public class MarkerManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "UI Marker & Ping Manager";

        // Serialized properties
        private SerializedProperty markerPrefabProp;
        private SerializedProperty closeRangeProp;
        private SerializedProperty farRangeProp;
        private SerializedProperty edgePaddingProp;
        private SerializedProperty defaultPingIconProp;
        private SerializedProperty pingLifetimeProp;
        private SerializedProperty pingCooldownProp;
        private SerializedProperty pingHeightOffsetProp;
        private SerializedProperty canvasRectProp;

        // Reflection caching for debug panel
        private System.Reflection.FieldInfo activeTargetsField;
        private System.Reflection.FieldInfo poolField;

        private void OnEnable()
        {
            markerPrefabProp = serializedObject.FindProperty("markerPrefab");
            closeRangeProp = serializedObject.FindProperty("closeRange");
            farRangeProp = serializedObject.FindProperty("farRange");
            edgePaddingProp = serializedObject.FindProperty("edgePadding");
            defaultPingIconProp = serializedObject.FindProperty("defaultPingIcon");
            pingLifetimeProp = serializedObject.FindProperty("pingLifetime");
            pingCooldownProp = serializedObject.FindProperty("pingCooldown");
            pingHeightOffsetProp = serializedObject.FindProperty("pingHeightOffset");
            canvasRectProp = serializedObject.FindProperty("canvasRect");

            // Cache reflection info for high performance debug inspection
            activeTargetsField = typeof(MarkerManager).GetField("activeTargets", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            poolField = typeof(MarkerManager).GetField("pool", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: Core Prefabs & Setup ──
            BeginCard("Prefab & Canvas Setup");
            {
                DrawProperty(markerPrefabProp, "Marker UI Prefab", "The MarkerUI template instantiated for each target marker.");
                if (markerPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("Marker UI Prefab is not assigned! The Marker System will not function.", MessageType.Error);
                }

                DrawProperty(canvasRectProp, "Canvas RectTransform", "The Canvas root where markers will be drawn. If left blank, it is automatically resolved on Awake.");
                if (canvasRectProp.objectReferenceValue == null)
                {
                    DrawMessage("Canvas RectTransform is blank. Will search parent GameObjects on startup.", MessageType.Info);
                }
            }
            EndCard();

            // ── Card 2: Distance Thresholds ──
            BeginCard("Distance Filtering");
            {
                // Range fields
                DrawProperty(closeRangeProp, "Close Range Threshold", "Distance within which the full name, icon, and health bar are shown.");
                DrawProperty(farRangeProp, "Far Range Threshold", "Distance above close range within which icon and distance are shown. Beyond this, only the icon is drawn.");

                float closeVal = closeRangeProp.floatValue;
                float farVal = farRangeProp.floatValue;

                // Validation warnings
                if (closeVal > farVal)
                {
                    DrawMessage("Close Range is greater than Far Range! Visual behavior will be erratic.", MessageType.Warning);
                }

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                // Detailed state descriptors to help designers
                GUIStyle labelHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                labelHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;

                GUIStyle textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11
                };
                textStyle.normal.textColor = MEEditorTheme.ColorTextMuted;

                GUILayout.Label("Range State Behavior Map:", labelHeaderStyle);
                GUILayout.Label($"• <b>0m to {closeVal:F0}m</b>: Show full details (Icon + Display Name + Health bar).", textStyle);
                GUILayout.Label($"• <b>{closeVal:F0}m to {farVal:F0}m</b>: Show brief details (Icon + Distance label).", textStyle);
                GUILayout.Label($"• <b>Beyond {farVal:F0}m</b>: Show minimal details (Icon only).", textStyle);
            }
            EndCard();

            // ── Card 3: Ping & Screen Settings ──
            BeginCard("Ping & Screen Constraints");
            {
                DrawProperty(defaultPingIconProp, "Default Ping Icon", "Sprite to display for temporary world pings.");

                var pingSprite = defaultPingIconProp.objectReferenceValue as Sprite;
                if (pingSprite != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                        Color oldBg = GUI.backgroundColor;
                        GUI.backgroundColor = MEEditorTheme.ColorWindowBg;
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(32), GUILayout.Height(32));
                        {
                            Rect rect = GUILayoutUtility.GetRect(24, 24);
                            DrawSprite(rect, pingSprite);
                        }
                        GUILayout.EndVertical();
                        GUI.backgroundColor = oldBg;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                }

                DrawProperty(pingLifetimeProp, "Ping Lifetime", "Time in seconds before a world ping is automatically cleared.");
                DrawProperty(pingCooldownProp, "Ping Cooldown", "Required delay between consecutive pings by a single player.");
                DrawProperty(pingHeightOffsetProp, "Ping Vertical Offset", "Meters to float the ping above its exact target coordinates.");

                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawProperty(edgePaddingProp, "Off-Screen Edge Padding", "Border in pixels from the canvas edges. Keeps off-screen target arrows on screen.");
            }
            EndCard();

            // ── Card 4: Play Mode Runtime Debugger ──
            if (EditorApplication.isPlaying)
            {
                BeginCard("Live Runtime Debugger");
                {
                    if (MarkerManager.Instance != null)
                    {
                        // Safely retrieve private collections via reflection
                        var activeTargetsList = activeTargetsField?.GetValue(MarkerManager.Instance) as List<MarkerTarget>;
                        var poolQueue = poolField?.GetValue(MarkerManager.Instance) as Queue<MarkerUI>;

                        int activeCount = activeTargetsList != null ? activeTargetsList.Count : 0;
                        int pooledCount = poolQueue != null ? poolQueue.Count : 0;

                        // Stats Summary Row
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
                            GUILayout.Label("<color=#5CACEE><b>ACTIVE TRACKS</b></color>", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
                            GUILayout.Label($"<size=18><b>{activeCount}</b></size>", new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, richText = true });
                            GUILayout.EndVertical();

                            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
                            GUILayout.Label("<color=#66CD00><b>POOLED UI ITEMS</b></color>", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
                            GUILayout.Label($"<size=18><b>{pooledCount}</b></size>", new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, richText = true });
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(8);

                        // Ping Test Button
                        if (GUILayout.Button("Spawn Mock Ping at Camera Center", MEEditorTheme.StylePrimaryButton))
                        {
                            Camera mainCam = Camera.main;
                            if (mainCam != null)
                            {
                                Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * 12f;
                                MarkerManager.Instance.PingAt(null, spawnPos, "Debug Ping");
                                Debug.Log($"[MarkerManagerEditor] Spawned debug ping at {spawnPos}");
                            }
                            else
                            {
                                Debug.LogWarning("[MarkerManagerEditor] Cannot spawn mock ping: No Camera.main found in scene.");
                            }
                        }

                        GUILayout.Space(8);

                        // Active markers lists
                        if (activeCount > 0)
                        {
                            GUILayout.Label("Active Markers List:", EditorStyles.boldLabel);
                            
                            GUIStyle listItemStyle = new GUIStyle(GUI.skin.box);
                            listItemStyle.normal.background = MEEditorTheme.GetTexture(new Color(0.18f, 0.20f, 0.24f));
                            listItemStyle.padding = new RectOffset(8, 8, 4, 4);

                            for (int i = 0; i < activeCount; i++)
                            {
                                MarkerTarget target = activeTargetsList[i];
                                if (target == null) continue;

                                GUILayout.BeginHorizontal(listItemStyle);
                                {
                                    Color cachedColor = GUI.color;
                                    GUI.color = target.MarkerColor;
                                    
                                    // Mini icon
                                    if (target.Icon != null)
                                    {
                                        Rect r = GUILayoutUtility.GetRect(16, 16);
                                        DrawSprite(r, target.Icon);
                                    }
                                    else
                                    {
                                        GUILayout.Label("■", GUILayout.Width(16));
                                    }

                                    GUI.color = cachedColor;

                                    GUILayout.Space(6);
                                    GUILayout.Label($"<b>{target.DisplayName}</b> (Pos: {target.transform.position})", new GUIStyle(EditorStyles.label) { richText = true });
                                    
                                    GUILayout.FlexibleSpace();
                                    
                                    if (GUILayout.Button("Focus", EditorStyles.miniButton, GUILayout.Width(50)))
                                    {
                                        Selection.activeGameObject = target.gameObject;
                                        SceneView.FrameLastActiveSceneView();
                                    }
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            GUILayout.Label("<i>No active markers currently tracked in scene.</i>", new GUIStyle(EditorStyles.label) { richText = true });
                        }
                    }
                    else
                    {
                        DrawMessage("MarkerManager Instance is inactive or disabled.", MessageType.Warning);
                    }
                }
                EndCard();
            }
        }

        // Helper to draw sprites perfectly inside GUI Rects
        private void DrawSprite(Rect position, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.textureRect;

            Rect texCoords = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = position.width / position.height;

            Rect drawRect = position;
            if (spriteAspect > rectAspect)
            {
                float newHeight = position.width / spriteAspect;
                drawRect.y += (position.height - newHeight) / 2f;
                drawRect.height = newHeight;
            }
            else
            {
                float newWidth = position.height * spriteAspect;
                drawRect.x += (position.width - newWidth) / 2f;
                drawRect.width = newWidth;
            }

            GUI.DrawTextureWithTexCoords(drawRect, tex, texCoords);
        }
    }
}