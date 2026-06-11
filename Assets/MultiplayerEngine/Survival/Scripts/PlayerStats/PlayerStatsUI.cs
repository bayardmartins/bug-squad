using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// UI for displaying player stats (Health, Stamina, Food, Water).
    /// Supports both Sliders and Radial/Fill Images.
    /// Only updates components that are assigned.
    /// </summary>
    public class PlayerStatsUI : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Slider bar for health (optional)")]
        [SerializeField] private Slider healthSlider;
        [Tooltip("Radial/fill image for health (optional)")]
        [SerializeField] private Image healthRadial;
        [Tooltip("Text display for health (optional)")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Color healthColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(0.6f, 0.1f, 0.1f);

        [Header("Stamina")]
        [Tooltip("Slider bar for stamina (optional)")]
        [SerializeField] private Slider staminaSlider;
        [Tooltip("Radial/fill image for stamina (optional)")]
        [SerializeField] private Image staminaRadial;
        [Tooltip("Text display for stamina (optional)")]
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private Color staminaColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color staminaLowColor = new Color(0.4f, 0.4f, 0.2f);

        [Header("Food")]
        [Tooltip("Slider bar for food (optional)")]
        [SerializeField] private Slider foodSlider;
        [Tooltip("Radial/fill image for food (optional)")]
        [SerializeField] private Image foodRadial;
        [Tooltip("Text display for food (optional)")]
        [SerializeField] private TMP_Text foodText;
        [SerializeField] private Color foodColor = new Color(0.9f, 0.6f, 0.2f);
        [SerializeField] private Color foodLowColor = new Color(0.6f, 0.3f, 0.1f);

        [Header("Water")]
        [Tooltip("Slider bar for water (optional)")]
        [SerializeField] private Slider waterSlider;
        [Tooltip("Radial/fill image for water (optional)")]
        [SerializeField] private Image waterRadial;
        [Tooltip("Text display for water (optional)")]
        [SerializeField] private TMP_Text waterText;
        [SerializeField] private Color waterColor = new Color(0.2f, 0.5f, 0.9f);
        [SerializeField] private Color waterLowColor = new Color(0.2f, 0.3f, 0.5f);

        [Header("Settings")]
        [SerializeField] private float lowThreshold = 0.25f;
        [SerializeField] private float smoothSpeed = 10f;

        // Reference (auto-set by PlayerStatsManager when owner spawns)
        private IPlayerStatsManager statsManager;

        // Smooth values (0-1 range for percentage)
        private float targetHealth, targetStamina, targetFood, targetWater;
        private float currentHealth, currentStamina, currentFood, currentWater;

        // Cached stats for text updates
        private PlayerStatsManager.PlayerStats cachedStats;

        /// <summary>
        /// Called by PlayerStatsManager when owner spawns.
        /// Only the local player's manager connects to this UI.
        /// </summary>
        public void SetStatsManager(IPlayerStatsManager manager)
        {
            if (statsManager != null)
            {
                statsManager.OnStatsChanged -= OnStatsChanged;
            }

            statsManager = manager;

            if (statsManager != null)
            {
                statsManager.OnStatsChanged += OnStatsChanged;
                
                var stats = statsManager.Stats;
                cachedStats = stats;
                
                // Initialize slider min/max values
                InitializeSliders(stats);
                
                targetHealth = stats.HealthPercent;
                targetStamina = stats.StaminaPercent;
                targetFood = stats.FoodPercent;
                targetWater = stats.WaterPercent;
                
                currentHealth = targetHealth;
                currentStamina = targetStamina;
                currentFood = targetFood;
                currentWater = targetWater;
                
                UpdateAllUI();
            }
        }

        private void InitializeSliders(PlayerStatsManager.PlayerStats stats)
        {
            // Set slider min/max values (sliders work with actual values, not percentages)
            if (healthSlider != null)
            {
                healthSlider.minValue = 0;
                healthSlider.maxValue = stats.maxHealth;
            }
            if (staminaSlider != null)
            {
                staminaSlider.minValue = 0;
                staminaSlider.maxValue = stats.maxStamina;
            }
            if (foodSlider != null)
            {
                foodSlider.minValue = 0;
                foodSlider.maxValue = stats.maxFood;
            }
            if (waterSlider != null)
            {
                waterSlider.minValue = 0;
                waterSlider.maxValue = stats.maxWater;
            }
        }

        private void Update()
        {
            if (statsManager == null) return;

            // Smooth lerp to target values
            currentHealth = Mathf.Lerp(currentHealth, targetHealth, smoothSpeed * Time.deltaTime);
            currentStamina = Mathf.Lerp(currentStamina, targetStamina, smoothSpeed * Time.deltaTime);
            currentFood = Mathf.Lerp(currentFood, targetFood, smoothSpeed * Time.deltaTime);
            currentWater = Mathf.Lerp(currentWater, targetWater, smoothSpeed * Time.deltaTime);

            UpdateBars();
        }

        private void OnStatsChanged(PlayerStatsManager.PlayerStats stats)
        {
            cachedStats = stats;
            targetHealth = stats.HealthPercent;
            targetStamina = stats.StaminaPercent;
            targetFood = stats.FoodPercent;
            targetWater = stats.WaterPercent;

            UpdateTexts();
        }

        private void UpdateBars()
        {
            // Health
            UpdateBar(healthSlider, healthRadial, cachedStats.health, currentHealth, healthColor, healthLowColor);
            
            // Stamina
            UpdateBar(staminaSlider, staminaRadial, cachedStats.stamina, currentStamina, staminaColor, staminaLowColor);
            
            // Food
            UpdateBar(foodSlider, foodRadial, cachedStats.food, currentFood, foodColor, foodLowColor);
            
            // Water
            UpdateBar(waterSlider, waterRadial, cachedStats.water, currentWater, waterColor, waterLowColor);
        }

        /// <summary>
        /// Updates slider (with actual value) or radial image (with percentage).
        /// </summary>
        private void UpdateBar(Slider slider, Image radial, float actualValue, float percentage, Color normalColor, Color lowColor)
        {
            Color color = percentage < lowThreshold ? lowColor : normalColor;

            // Update slider if assigned (uses actual value, not percentage)
            if (slider != null)
            {
                slider.value = actualValue;
                
                // Color the fill area
                if (slider.fillRect != null)
                {
                    var fillImage = slider.fillRect.GetComponent<Image>();
                    if (fillImage != null)
                        fillImage.color = color;
                }
            }

            // Update radial image if assigned (uses percentage 0-1)
            if (radial != null)
            {
                radial.fillAmount = percentage;
                radial.color = color;
            }
        }

        private void UpdateTexts()
        {
            // Only update text if assigned
            if (healthText != null)
                healthText.text = $"{Mathf.CeilToInt(cachedStats.health)}/{Mathf.CeilToInt(cachedStats.maxHealth)}";
            
            if (staminaText != null)
                staminaText.text = $"{Mathf.CeilToInt(cachedStats.stamina)}/{Mathf.CeilToInt(cachedStats.maxStamina)}";
            
            if (foodText != null)
                foodText.text = $"{Mathf.CeilToInt(cachedStats.food)}/{Mathf.CeilToInt(cachedStats.maxFood)}";
            
            if (waterText != null)
                waterText.text = $"{Mathf.CeilToInt(cachedStats.water)}/{Mathf.CeilToInt(cachedStats.maxWater)}";
        }

        private void UpdateAllUI()
        {
            UpdateBars();
            UpdateTexts();
        }

        private void OnDestroy()
        {
            if (statsManager != null)
            {
                statsManager.OnStatsChanged -= OnStatsChanged;
            }
        }
    }
}