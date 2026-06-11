#if UNITY_EDITOR && (UNITY_SERVICES || STEAM_SERVICES)
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine.Editor
{
    /// <summary>
    /// Premium custom editor for PlayerStatsManager leveraging the universal MEEditorInspector base.
    /// Provides standardized cards and clean vitals progress bars in playmode.
    /// </summary>
    [CustomEditor(typeof(PlayerStatsManager))]
    public class PlayerStatsManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Core Attributes & Vitals Manager";

        // Base Stats
        private SerializedProperty baseMaxHealth;
        private SerializedProperty baseMaxStamina;
        private SerializedProperty baseMaxFood;
        private SerializedProperty baseMaxWater;

        // Regeneration
        private SerializedProperty healthRegenRate;
        private SerializedProperty healthRegenRateFast;
        private SerializedProperty staminaRegenRate;
        private SerializedProperty regenDelay;

        // Health Regen Thresholds
        private SerializedProperty healthRegenThreshold;
        private SerializedProperty healthRegenFastThreshold;

        // Drain Rates
        private SerializedProperty foodDrainRate;
        private SerializedProperty waterDrainRate;
        private SerializedProperty runningStaminaDrain;

        // Starvation / Dehydration
        private SerializedProperty lowThreshold;
        private SerializedProperty starvationDamage;

        // Network Optimization
        private SerializedProperty syncThreshold;
        private SerializedProperty syncInterval;

        // Hit Reaction
        private SerializedProperty invincibilityDuration;
        private SerializedProperty bloodHitEffectPrefab;

        // Debug
        private SerializedProperty debugRunning;

        private void OnEnable()
        {
            baseMaxHealth = serializedObject.FindProperty("baseMaxHealth");
            baseMaxStamina = serializedObject.FindProperty("baseMaxStamina");
            baseMaxFood = serializedObject.FindProperty("baseMaxFood");
            baseMaxWater = serializedObject.FindProperty("baseMaxWater");

            healthRegenRate = serializedObject.FindProperty("healthRegenRate");
            healthRegenRateFast = serializedObject.FindProperty("healthRegenRateFast");
            staminaRegenRate = serializedObject.FindProperty("staminaRegenRate");
            regenDelay = serializedObject.FindProperty("regenDelay");

            healthRegenThreshold = serializedObject.FindProperty("healthRegenThreshold");
            healthRegenFastThreshold = serializedObject.FindProperty("healthRegenFastThreshold");

            foodDrainRate = serializedObject.FindProperty("foodDrainRate");
            waterDrainRate = serializedObject.FindProperty("waterDrainRate");
            runningStaminaDrain = serializedObject.FindProperty("runningStaminaDrain");

            lowThreshold = serializedObject.FindProperty("lowThreshold");
            starvationDamage = serializedObject.FindProperty("starvationDamage");

            syncThreshold = serializedObject.FindProperty("syncThreshold");
            syncInterval = serializedObject.FindProperty("syncInterval");

            invincibilityDuration = serializedObject.FindProperty("invincibilityDuration");
            bloodHitEffectPrefab = serializedObject.FindProperty("bloodHitEffectPrefab");

            debugRunning = serializedObject.FindProperty("debugRunning");
        }

        protected override void DrawInspectorBody()
        {
            // ── Vitals Monitor & Preview (At the very start) ──
            var manager = (PlayerStatsManager)target;

            BeginCard(EditorApplication.isPlaying ? "Live Vitals Monitor" : "Live Vitals Preview");
            {
                // Defined cohesive vital colors matching universal PlayerStats UI defaults
                Color healthColor = new Color(0.8f, 0.2f, 0.2f, 1f);
                Color staminaColor = new Color(0.2f, 0.8f, 0.3f, 1f);
                Color foodColor = new Color(0.9f, 0.6f, 0.2f, 1f);
                Color waterColor = new Color(0.2f, 0.5f, 0.9f, 1f);

                if (EditorApplication.isPlaying)
                {
                    var stats = manager.Stats;
                    bool isAlive = manager.IsAlive;

                    if (!isAlive)
                    {
                        DrawMessage("Character is Dead!", MessageType.Error);
                    }

                    DrawStatProgressBar("Health", stats.health, stats.maxHealth, healthColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Stamina", stats.stamina, stats.maxStamina, staminaColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Food", stats.food, stats.maxFood, foodColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Water", stats.water, stats.maxWater, waterColor);

                    GUILayout.Space(6);
                    GUILayout.Label($"<b>Action Allowances</b>: Can Act: {(manager.CanAct ? "Yes" : "No")}  |  Can Run: {(manager.CanRun ? "Yes" : "No")}", new GUIStyle(EditorStyles.miniLabel) { richText = true });

                    GUILayout.Space(8);
                    if (GUILayout.Button("Reset Vitals to Maximum", GUILayout.Height(24)))
                    {
                        manager.ResetStatsDebug();
                        Debug.Log("[PlayerStatsManagerEditor] Reset stats to default maximums.");
                    }
                }
                else
                {
                    // Edit mode - show configured max values at 100%
                    float maxHealth = baseMaxHealth.floatValue;
                    float maxStamina = baseMaxStamina.floatValue;
                    float maxFood = baseMaxFood.floatValue;
                    float maxWater = baseMaxWater.floatValue;

                    DrawStatProgressBar("Health", maxHealth, maxHealth, healthColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Stamina", maxStamina, maxStamina, staminaColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Food", maxFood, maxFood, foodColor);
                    GUILayout.Space(2);
                    DrawStatProgressBar("Water", maxWater, maxWater, waterColor);
                }
            }
            EndCard();
            GUILayout.Space(8);

            // ── Base Stats Configuration ──
            BeginCard("Base Attribute Settings");
            {
                DrawProperty(baseMaxHealth, "Max Health", "Default starting health value.");
                DrawProperty(baseMaxStamina, "Max Stamina", "Default starting stamina value.");
                DrawProperty(baseMaxFood, "Max Food", "Default starting food value.");
                DrawProperty(baseMaxWater, "Max Water", "Default starting water value.");
            }
            EndCard();

            // ── Regeneration Rates ──
            BeginCard("Regeneration & Thresholds");
            {
                DrawProperty(healthRegenRate, "Normal Health Regen", "HP restored per second when vitals are above threshold.");
                DrawProperty(healthRegenRateFast, "Fast Health Regen", "HP restored per second when vitals are above fast threshold.");
                DrawProperty(healthRegenThreshold, "Normal Threshold", "Vitals percentage required to trigger normal health regen.");
                DrawProperty(healthRegenFastThreshold, "Fast Threshold", "Vitals percentage required to trigger fast health regen.");
                
                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawProperty(staminaRegenRate, "Stamina Regen Rate", "Stamina restored per second when recovering.");
                DrawProperty(regenDelay, "Regen Cooldown Delay", "Time delay in seconds before stamina recovery resumes after exertion.");
            }
            EndCard();

            // ── Drain Rates ──
            BeginCard("Exertion & Vitals Drain");
            {
                DrawProperty(foodDrainRate, "Food Drain Rate", "Rate at which food is consumed per second.");
                DrawProperty(waterDrainRate, "Water Drain Rate", "Rate at which water is consumed per second.");
                DrawProperty(runningStaminaDrain, "Sprint Cost Rate", "Stamina consumed per second while sprinting.");

                // Estimated survival statistics
                float foodSeconds = baseMaxFood.floatValue / Mathf.Max(foodDrainRate.floatValue, 0.001f);
                float waterSeconds = baseMaxWater.floatValue / Mathf.Max(waterDrainRate.floatValue, 0.001f);
                DrawMessage($"Survival estimates from full:\n• Food depletion: {FormatTime(foodSeconds)}\n• Water depletion: {FormatTime(waterSeconds)}", MessageType.Info);
            }
            EndCard();

            // ── Starvation & Dehydration ──
            BeginCard("Starvation & Penalties");
            {
                DrawProperty(lowThreshold, "Vitals Low Limit", "Vitals levels below which character penalties apply.");
                DrawProperty(starvationDamage, "Starvation DPS", "HP damage received per second when food or water is fully depleted.");
            }
            EndCard();

            // ── Network Synch ──
            BeginCard("Network Optimization Settings");
            {
                DrawProperty(syncThreshold, "Sync Sensitivity", "Minimum vitals difference before triggering standard network sync.");
                DrawProperty(syncInterval, "Maximum Sync Sync", "Maximum interval threshold in seconds before forcing a network update.");
            }
            EndCard();

            // ── Hit Reaction ──
            BeginCard("Combat Vitals Reaction");
            {
                DrawProperty(invincibilityDuration, "Invincibility Duration", "Safety delay in seconds before player can receive damage again.");
                DrawProperty(bloodHitEffectPrefab, "Blood Hit Effect Prefab", "Custom hit particle effect (e.g. blood splash) spawned when this player takes damage.");
            }
            EndCard();

            // ── Debug Options ──
            BeginCard("Diagnostics & Testing");
            {
                DrawProperty(debugRunning, "Simulate Exertion", "Forces stamina drain in the editor to simulate sprint state.");
            }
            EndCard();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawStatProgressBar(string label, float current, float max, Color fillColor)
        {
            float percent = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            string displayVal = $"{current:F1} / {max:F0} ({percent * 100f:F0}%)";
            
            // Draw clean, premium custom progress bar matching unified editor styles
            Rect barRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            
            // Background bar (sleek dark tone)
            EditorGUI.DrawRect(barRect, new Color(0.12f, 0.12f, 0.14f, 1f));
            
            // Colored fill
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * percent, barRect.height);
            EditorGUI.DrawRect(fillRect, fillColor);
            
            // 1px Border mapping card styling
            Color borderColor = new Color(0.25f, 0.25f, 0.28f, 0.6f);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), borderColor); // top
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1, barRect.width, 1), borderColor); // bottom
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, 1, barRect.height), borderColor); // left
            EditorGUI.DrawRect(new Rect(barRect.xMax - 1, barRect.y, 1, barRect.height), borderColor); // right
            
            // Text values overlaid cleanly
            GUIStyle textStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
            textStyle.normal.textColor = Color.white;
            
            GUI.Label(barRect, $"{label}: {displayVal}", textStyle);
        }

        private string FormatTime(float seconds)
        {
            if (seconds >= 3600f)
                return $"{seconds / 3600f:F1} hours";
            if (seconds >= 60f)
                return $"{seconds / 60f:F1} minutes";
            return $"{seconds:F0} seconds";
        }
    }
}
#endif
