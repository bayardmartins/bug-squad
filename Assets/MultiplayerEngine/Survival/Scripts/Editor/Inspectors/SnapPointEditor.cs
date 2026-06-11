using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for SnapPoint that provides a clean, premium visual drawing
    /// of the snap compatibility rules, resolving standard Unity array clutter
    /// and providing helpful quick presets and diagnostics.
    /// </summary>
    [CustomEditor(typeof(SnapPoint))]
    [CanEditMultipleObjects]
    public class SnapPointEditor : UnityEditor.Editor
    {
        private SerializedProperty snapTypeProp;
        private SerializedProperty canSnapWithProp;

        // Cached styles
        private static Texture2D headerTex;
        private static Texture2D sectionBgTex;
        private static Texture2D darkBgTex;
        private static GUIStyle headerStyle;
        private static GUIStyle sectionStyle;
        private static bool stylesInitialized;

        // Colors
        private static readonly Color HeaderColorStart = new Color(0.1f, 0.45f, 0.55f); // Teal/Cyan theme for Snaps
        private static readonly Color HeaderColorEnd = new Color(0.05f, 0.3f, 0.4f);
        private static readonly Color SectionBgColor = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color DarkBgColor = new Color(0.13f, 0.13f, 0.13f);
        private static readonly Color AccentTeal = new Color(0.2f, 0.75f, 0.85f);
        private static readonly Color AccentRed = new Color(0.85f, 0.3f, 0.3f);
        private static readonly Color LabelColor = new Color(0.75f, 0.75f, 0.75f);

        private void OnEnable()
        {
            snapTypeProp = serializedObject.FindProperty("snapType");
            canSnapWithProp = serializedObject.FindProperty("canSnapWith");
        }

        private void InitStyles()
        {
            if (stylesInitialized && headerTex != null) return;

            headerTex = new Texture2D(1, 32);
            for (int y = 0; y < 32; y++)
            {
                float t = y / 31f;
                headerTex.SetPixel(0, y, Color.Lerp(HeaderColorEnd, HeaderColorStart, t));
            }
            headerTex.Apply();

            sectionBgTex = MakeTex(2, 2, SectionBgColor);
            darkBgTex = MakeTex(2, 2, DarkBgColor);

            headerStyle = new GUIStyle()
            {
                normal = { background = headerTex, textColor = Color.white },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(0, 0, 8, 4)
            };

            sectionStyle = new GUIStyle()
            {
                normal = { background = sectionBgTex },
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            SnapPoint snapPoint = (SnapPoint)target;

            // Header Banner
            DrawSnapPointHeader();

            GUILayout.Space(4);

            // Identity Section
            GUILayout.Label("SNAP POINT IDENTITY", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                EditorGUILayout.PropertyField(snapTypeProp, new GUIContent("Snap Type", "The identity classification of this snap point. Other snap points will check this value."));
                EditorGUILayout.HelpBox("This snap point is classified as: " + (SnapType)snapTypeProp.enumValueIndex, MessageType.None);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(4);

            // Compatibility Section
            GUILayout.Label("SNAP COMPATIBILITY CONFIGURATION", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                // Check if empty
                if (canSnapWithProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("This snap point cannot snap to anything yet! Add at least one compatibility rule below.", MessageType.Warning);
                }
                else
                {
                    // Validation - Check for duplicate types
                    HashSet<SnapType> configuredTypes = new HashSet<SnapType>();
                    bool hasDuplicate = false;
                    for (int i = 0; i < canSnapWithProp.arraySize; i++)
                    {
                        var element = canSnapWithProp.GetArrayElementAtIndex(i);
                        var targetTypeProp = element.FindPropertyRelative("targetType");
                        if (targetTypeProp != null)
                        {
                            SnapType type = (SnapType)targetTypeProp.enumValueIndex;
                            if (configuredTypes.Contains(type))
                            {
                                hasDuplicate = true;
                            }
                            else
                            {
                                configuredTypes.Add(type);
                            }
                        }
                    }

                    if (hasDuplicate)
                    {
                        EditorGUILayout.HelpBox("Duplicate Target Types detected! Multiple rules for the same Snap Type can cause unpredictable behavior.", MessageType.Warning);
                    }
                }

                // Render each list element beautifully
                for (int i = 0; i < canSnapWithProp.arraySize; i++)
                {
                    var element = canSnapWithProp.GetArrayElementAtIndex(i);
                    DrawCompatibilityElement(element, i);
                    GUILayout.Space(8);
                }

                GUILayout.Space(4);

                // Add Compatibility button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = AccentTeal;
                if (GUILayout.Button("+ Add Compatibility Rule", GUILayout.Width(220), GUILayout.Height(30)))
                {
                    canSnapWithProp.arraySize++;
                    var newElement = canSnapWithProp.GetArrayElementAtIndex(canSnapWithProp.arraySize - 1);
                    // Reset to defaults
                    newElement.FindPropertyRelative("targetType").enumValueIndex = 0;
                    newElement.FindPropertyRelative("matchingRule").enumValueIndex = 0;
                    newElement.FindPropertyRelative("rotationPivotMode").enumValueIndex = 0;
                    newElement.FindPropertyRelative("rotationStep").floatValue = 90f;
                    newElement.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.9f;
                }
                GUI.backgroundColor = oldBg;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(4);

            // Preset Buttons Section
            GUILayout.Label("QUICK PRESETS", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                EditorGUILayout.HelpBox("Instantly populate snap settings using common presets to save configuration time.", MessageType.Info);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Floor Preset", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("Apply Floor Preset?", "This will overwrite existing snap compatibility settings with the standard Floor preset.", "Apply", "Cancel"))
                    {
                        ApplyFloorPreset();
                    }
                }
                if (GUILayout.Button("Wall Preset", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("Apply Wall Preset?", "This will overwrite existing snap compatibility settings with the standard Wall preset.", "Apply", "Cancel"))
                    {
                        ApplyWallPreset();
                    }
                }
                if (GUILayout.Button("Roof Preset", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("Apply Roof Preset?", "This will overwrite existing snap compatibility settings with the standard Roof preset.", "Apply", "Cancel"))
                    {
                        ApplyRoofPreset();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSnapPointHeader()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.BeginHorizontal();

            // Nice circle badge representing snap point
            Rect iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
            DrawSnapBadge(iconRect, AccentTeal);

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Snap Point Connector", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.white } });
                EditorGUILayout.LabelField("Configures snap alignment, rotation constraints, and stability rules.", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSnapBadge(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, 15f);
            Handles.color = Color.white;
            Handles.DrawWireDisc(rect.center, Vector3.forward, 15f, 2f);
            
            // Draw a small plus in the center
            Handles.DrawLine(rect.center - new Vector2(6, 0), rect.center + new Vector2(6, 0), 2f);
            Handles.DrawLine(rect.center - new Vector2(0, 6), rect.center + new Vector2(0, 6), 2f);
            Handles.EndGUI();
        }

        private void DrawCompatibilityElement(SerializedProperty element, int index)
        {
            var targetTypeProp = element.FindPropertyRelative("targetType");
            var matchingRuleProp = element.FindPropertyRelative("matchingRule");
            var rotationPivotModeProp = element.FindPropertyRelative("rotationPivotMode");
            var rotationStepProp = element.FindPropertyRelative("rotationStep");
            var stabilityDecayFactorProp = element.FindPropertyRelative("stabilityDecayFactor");

            // Element Container Style
            GUIStyle containerStyle = new GUIStyle()
            {
                normal = { background = MakeTex(2, 2, DarkBgColor) },
                padding = new RectOffset(10, 10, 8, 8)
            };

            EditorGUILayout.BeginVertical(containerStyle);
            {
                // Title and Delete Row
                EditorGUILayout.BeginHorizontal();
                
                // Dot icon before title
                Rect dotRect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.BeginGUI();
                    Handles.color = AccentTeal;
                    Handles.DrawSolidDisc(dotRect.center, Vector3.forward, 4f);
                    Handles.EndGUI();
                }

                string typeName = targetTypeProp != null ? ((SnapType)targetTypeProp.enumValueIndex).ToString() : "Rule";
                string ruleTitle = $"Rule #{index + 1}: Snaps With [{typeName}]";
                EditorGUILayout.LabelField(ruleTitle, EditorStyles.boldLabel);
                
                // Delete button
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = AccentRed;
                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    int initialSize = canSnapWithProp.arraySize;
                    canSnapWithProp.DeleteArrayElementAtIndex(index);
                    if (canSnapWithProp.arraySize == initialSize)
                    {
                        canSnapWithProp.DeleteArrayElementAtIndex(index); // Standard Unity double-delete workaround
                    }
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldBg;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);
                
                // Draw fields cleanly
                EditorGUI.indentLevel++;
                
                if (targetTypeProp != null) EditorGUILayout.PropertyField(targetTypeProp, new GUIContent("Target Type", "The other SnapPoint's SnapType we can connect to."));
                if (matchingRuleProp != null) EditorGUILayout.PropertyField(matchingRuleProp, new GUIContent("Matching Rule", "How to physically align the piece when snapped."));
                if (rotationPivotModeProp != null) EditorGUILayout.PropertyField(rotationPivotModeProp, new GUIContent("Rotation Pivot", "SnapPoint: spins around connection. CenterPoint: spins around piece center."));
                if (rotationStepProp != null) rotationStepProp.floatValue = EditorGUILayout.Slider(new GUIContent("Rotation Step", "Degrees rotated per keystroke (e.g. 90 deg for 4 directions)."), rotationStepProp.floatValue, 0f, 360f);
                if (stabilityDecayFactorProp != null) stabilityDecayFactorProp.floatValue = EditorGUILayout.Slider(new GUIContent("Stability Decay", "Decay factor (0 to 1). 1.0 is no decay, 0.9 is 10% decay."), stabilityDecayFactorProp.floatValue, 0f, 1f);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void ApplyFloorPreset()
        {
            canSnapWithProp.ClearArray();
            canSnapWithProp.arraySize = 2;

            // 1. Can snap to other floors (Face-to-Face)
            var floorRule = canSnapWithProp.GetArrayElementAtIndex(0);
            floorRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.FloorEdge;
            floorRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Face;
            floorRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.SnapPoint;
            floorRule.FindPropertyRelative("rotationStep").floatValue = 90f;
            floorRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.9f;

            // 2. Can snap to walls (Face-to-Object)
            var wallRule = canSnapWithProp.GetArrayElementAtIndex(1);
            wallRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.WallBottom;
            wallRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Object;
            wallRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.CenterPoint;
            wallRule.FindPropertyRelative("rotationStep").floatValue = 90f;
            wallRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.9f;
        }

        private void ApplyWallPreset()
        {
            canSnapWithProp.ClearArray();
            canSnapWithProp.arraySize = 3;

            // 1. Wall bottom snaps to floor edge
            var bottomRule = canSnapWithProp.GetArrayElementAtIndex(0);
            bottomRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.FloorEdge;
            bottomRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Object;
            bottomRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.CenterPoint;
            bottomRule.FindPropertyRelative("rotationStep").floatValue = 90f;
            bottomRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.9f;

            // 2. Wall top snaps to wall bottom (stacking walls)
            var topRule = canSnapWithProp.GetArrayElementAtIndex(1);
            topRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.WallBottom;
            topRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Face;
            topRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.SnapPoint;
            topRule.FindPropertyRelative("rotationStep").floatValue = 180f;
            topRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.8f;

            // 3. Wall side snaps to other wall side
            var sideRule = canSnapWithProp.GetArrayElementAtIndex(2);
            sideRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.WallSide;
            sideRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Face;
            sideRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.SnapPoint;
            sideRule.FindPropertyRelative("rotationStep").floatValue = 90f;
            sideRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.85f;
        }

        private void ApplyRoofPreset()
        {
            canSnapWithProp.ClearArray();
            canSnapWithProp.arraySize = 2;

            // 1. Roof bottom snaps to wall top
            var wallRule = canSnapWithProp.GetArrayElementAtIndex(0);
            wallRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.WallTop;
            wallRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Object;
            wallRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.CenterPoint;
            wallRule.FindPropertyRelative("rotationStep").floatValue = 180f;
            wallRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.8f;

            // 2. Roof snaps to other roof edges
            var roofRule = canSnapWithProp.GetArrayElementAtIndex(1);
            roofRule.FindPropertyRelative("targetType").enumValueIndex = (int)SnapType.RoofEdge;
            roofRule.FindPropertyRelative("matchingRule").enumValueIndex = (int)MatchingRule.Face2Face;
            roofRule.FindPropertyRelative("rotationPivotMode").enumValueIndex = (int)RotationPivotMode.SnapPoint;
            roofRule.FindPropertyRelative("rotationStep").floatValue = 90f;
            roofRule.FindPropertyRelative("stabilityDecayFactor").floatValue = 0.85f;
        }
    }
}
