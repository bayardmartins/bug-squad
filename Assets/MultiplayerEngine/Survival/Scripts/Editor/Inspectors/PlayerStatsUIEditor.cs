using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for PlayerStatsUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Provides component validation alerts and playmode live tracking.
    /// </summary>
    [CustomEditor(typeof(PlayerStatsUI))]
    public class PlayerStatsUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "HUD Stats UI Display";

        // Health properties
        private SerializedProperty healthSliderProp;
        private SerializedProperty healthRadialProp;
        private SerializedProperty healthTextProp;
        private SerializedProperty healthColorProp;
        private SerializedProperty healthLowColorProp;

        // Stamina properties
        private SerializedProperty staminaSliderProp;
        private SerializedProperty staminaRadialProp;
        private SerializedProperty staminaTextProp;
        private SerializedProperty staminaColorProp;
        private SerializedProperty staminaLowColorProp;

        // Food properties
        private SerializedProperty foodSliderProp;
        private SerializedProperty foodRadialProp;
        private SerializedProperty foodTextProp;
        private SerializedProperty foodColorProp;
        private SerializedProperty foodLowColorProp;

        // Water properties
        private SerializedProperty waterSliderProp;
        private SerializedProperty waterRadialProp;
        private SerializedProperty waterTextProp;
        private SerializedProperty waterColorProp;
        private SerializedProperty waterLowColorProp;

        // Settings properties
        private SerializedProperty lowThresholdProp;
        private SerializedProperty smoothSpeedProp;

        // Reflection caching for runtime stats manager
        private System.Reflection.FieldInfo statsManagerField;
        private System.Reflection.FieldInfo currentHealthField;
        private System.Reflection.FieldInfo targetHealthField;
        private System.Reflection.FieldInfo currentStaminaField;
        private System.Reflection.FieldInfo targetStaminaField;
        private System.Reflection.FieldInfo currentFoodField;
        private System.Reflection.FieldInfo targetFoodField;
        private System.Reflection.FieldInfo currentWaterField;
        private System.Reflection.FieldInfo targetWaterField;

        private void OnEnable()
        {
            // Health
            healthSliderProp = serializedObject.FindProperty("healthSlider");
            healthRadialProp = serializedObject.FindProperty("healthRadial");
            healthTextProp = serializedObject.FindProperty("healthText");
            healthColorProp = serializedObject.FindProperty("healthColor");
            healthLowColorProp = serializedObject.FindProperty("healthLowColor");

            // Stamina
            staminaSliderProp = serializedObject.FindProperty("staminaSlider");
            staminaRadialProp = serializedObject.FindProperty("staminaRadial");
            staminaTextProp = serializedObject.FindProperty("staminaText");
            staminaColorProp = serializedObject.FindProperty("staminaColor");
            staminaLowColorProp = serializedObject.FindProperty("staminaLowColor");

            // Food
            foodSliderProp = serializedObject.FindProperty("foodSlider");
            foodRadialProp = serializedObject.FindProperty("foodRadial");
            foodTextProp = serializedObject.FindProperty("foodText");
            foodColorProp = serializedObject.FindProperty("foodColor");
            foodLowColorProp = serializedObject.FindProperty("foodLowColor");

            // Water
            waterSliderProp = serializedObject.FindProperty("waterSlider");
            waterRadialProp = serializedObject.FindProperty("waterRadial");
            waterTextProp = serializedObject.FindProperty("waterText");
            waterColorProp = serializedObject.FindProperty("waterColor");
            waterLowColorProp = serializedObject.FindProperty("waterLowColor");

            // Settings
            lowThresholdProp = serializedObject.FindProperty("lowThreshold");
            smoothSpeedProp = serializedObject.FindProperty("smoothSpeed");

            // Cache reflection info for runtime stats tracker
            statsManagerField = typeof(PlayerStatsUI).GetField("statsManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentHealthField = typeof(PlayerStatsUI).GetField("currentHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetHealthField = typeof(PlayerStatsUI).GetField("targetHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentStaminaField = typeof(PlayerStatsUI).GetField("currentStamina", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetStaminaField = typeof(PlayerStatsUI).GetField("targetStamina", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentFoodField = typeof(PlayerStatsUI).GetField("currentFood", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetFoodField = typeof(PlayerStatsUI).GetField("targetFood", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentWaterField = typeof(PlayerStatsUI).GetField("currentWater", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetWaterField = typeof(PlayerStatsUI).GetField("targetWater", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: Health UI Components ──
            DrawStatUISection("Health UI Setup", healthSliderProp, healthRadialProp, healthTextProp, healthColorProp, healthLowColorProp, new Color(0.85f, 0.22f, 0.25f));

            // ── Card 2: Stamina UI Components ──
            DrawStatUISection("Stamina UI Setup", staminaSliderProp, staminaRadialProp, staminaTextProp, staminaColorProp, staminaLowColorProp, new Color(0.95f, 0.75f, 0.15f));

            // ── Card 3: Food UI Components ──
            DrawStatUISection("Food UI Setup", foodSliderProp, foodRadialProp, foodTextProp, foodColorProp, foodLowColorProp, new Color(0.9f, 0.6f, 0.2f));

            // ── Card 4: Water UI Components ──
            DrawStatUISection("Water UI Setup", waterSliderProp, waterRadialProp, waterTextProp, waterColorProp, waterLowColorProp, new Color(0.2f, 0.5f, 0.9f));

            // ── Card 5: General Visual Settings ──
            BeginCard("Visual Animation Settings");
            {
                // Low Threshold Slider using standard EditorGUILayout.Slider
                lowThresholdProp.floatValue = EditorGUILayout.Slider(
                    new GUIContent("Low Stat Threshold", "Threshold (0-1 percentage) below which the fill bars transition to their low colors."), 
                    lowThresholdProp.floatValue, 0f, 1f);

                DrawProperty(smoothSpeedProp, "Smooth Speed", "The interpolation speed used to smoothly transition UI bar values towards actual values.");
            }
            EndCard();

            // ── Card 6: Play Mode Runtime Monitor ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        /// <summary>
        /// Helper to draw visual setting configurations for a specific player statistic.
        /// </summary>
        private void DrawStatUISection(string cardTitle, SerializedProperty sliderProp, SerializedProperty radialProp, 
                                       SerializedProperty textProp, SerializedProperty normalColorProp, SerializedProperty lowColorProp, Color cardAccent)
        {
            BeginCard(cardTitle);
            {
                DrawProperty(sliderProp, "Slider UI Bar", "Slider component used to represent stats via fills (optional).");
                DrawProperty(radialProp, "Radial/Fill Image", "Image UI component using radial, horizontal, or vertical fill methods (optional).");

                // Warning if neither slider nor radial fill is assigned
                if (sliderProp.objectReferenceValue == null && radialProp.objectReferenceValue == null)
                {
                    DrawMessage("Neither Slider nor Radial image is assigned. Fill bars will not be visible in game.", MessageType.Warning);
                }

                DrawProperty(textProp, "Text Display (TMP)", "TextMeshPro text block used to display actual/max values text (optional).");

                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                // Stack color pickers vertically to prevent off-screen clipping in narrow panels
                DrawProperty(normalColorProp, "Normal Color", "Color of the fill bar under normal conditions.");
                DrawProperty(lowColorProp, "Low Color", "Color of the fill bar when the stat level falls below the low threshold.");
            }
            EndCard();
        }

        /// <summary>
        /// Renders live stats readouts in Play Mode.
        /// </summary>
        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Stats UI Monitor");
            {
                var ui = (PlayerStatsUI)target;
                var statsManager = statsManagerField?.GetValue(ui);

                if (statsManager != null)
                {
                    // Fetch live lerped and target values
                    float curHP = (float)(currentHealthField?.GetValue(ui) ?? 0f);
                    float tarHP = (float)(targetHealthField?.GetValue(ui) ?? 0f);

                    float curStam = (float)(currentStaminaField?.GetValue(ui) ?? 0f);
                    float tarStam = (float)(targetStaminaField?.GetValue(ui) ?? 0f);

                    float curFood = (float)(currentFoodField?.GetValue(ui) ?? 0f);
                    float tarFood = (float)(targetFoodField?.GetValue(ui) ?? 0f);

                    float curWater = (float)(currentWaterField?.GetValue(ui) ?? 0f);
                    float tarWater = (float)(targetWaterField?.GetValue(ui) ?? 0f);

                    float lowLvl = lowThresholdProp.floatValue;

                    // Display Progress Rows
                    DrawLiveBar("Health", curHP, tarHP, lowLvl, healthColorProp.colorValue, healthLowColorProp.colorValue);
                    GUILayout.Space(4);
                    DrawLiveBar("Stamina", curStam, tarStam, lowLvl, staminaColorProp.colorValue, staminaLowColorProp.colorValue);
                    GUILayout.Space(4);
                    DrawLiveBar("Food", curFood, tarFood, lowLvl, foodColorProp.colorValue, foodLowColorProp.colorValue);
                    GUILayout.Space(4);
                    DrawLiveBar("Water", curWater, tarWater, lowLvl, waterColorProp.colorValue, waterLowColorProp.colorValue);

                    // Forces editor scene views to refresh immediately
                    Repaint();
                }
                else
                {
                    DrawMessage("No active IPlayerStatsManager assigned to this UI (occurs when owner player has not spawned yet).", MessageType.Info);
                }
            }
            EndCard();
        }

        /// <summary>
        /// Draws a styled representation of a live stat value in the editor layout.
        /// </summary>
        private void DrawLiveBar(string label, float currentPercent, float targetPercent, float lowLvl, Color normalColor, Color lowColor)
        {
            Rect barRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            
            // Draw background fill
            EditorGUI.DrawRect(barRect, new Color(0.1f, 0.12f, 0.15f));

            // Choose bar color depending on low threshold
            Color fillCol = currentPercent < lowLvl ? lowColor : normalColor;

            // Draw current lerped value fill
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * currentPercent, barRect.height);
            EditorGUI.DrawRect(fillRect, fillCol);

            // Draw a subtle vertical tick for target value
            Rect tickRect = new Rect(barRect.x + barRect.width * targetPercent - 1, barRect.y, 2, barRect.height);
            EditorGUI.DrawRect(tickRect, Color.white);

            // Add text overlays
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = MEEditorTheme.ColorTextNormal;

            GUIStyle valueStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };
            valueStyle.normal.textColor = MEEditorTheme.ColorTextNormal;

            GUI.Label(new Rect(barRect.x + 6, barRect.y, barRect.width * 0.4f, barRect.height), label, labelStyle);
            GUI.Label(new Rect(barRect.x + barRect.width * 0.4f, barRect.y, barRect.width * 0.56f, barRect.height), 
                $"Lerped: {currentPercent * 100:F0}%  (Target: {targetPercent * 100:F0}%)", valueStyle);

            // Draw 1px border around the entire bar
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, 1, barRect.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(barRect.xMax - 1, barRect.y, 1, barRect.height), new Color(0f, 0f, 0f, 0.35f));
        }
    }
}