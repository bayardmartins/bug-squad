using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    public enum WeatherState
    {
        Clear = 0,
        Cloudy = 1,
        Rain = 2,
        Storm = 3
    }

    /// <summary>
    /// Server-authoritative unified day/night cycle + weather controller.
    /// Weather modifiers are applied on top of base day/night values so they
    /// always work together — rain at night looks different from rain at noon.
    /// </summary>
    public class EnvironmentManager : NetworkBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        #region Configuration

        [Header("Day/Night Cycle")]
        [Tooltip("Total real-time seconds for one full day cycle")]
        [SerializeField] private float dayDurationInSeconds = 720f; // 12 minutes
        [Tooltip("Starting time of day (0=midnight, 0.5=noon, 1=midnight)")]
        [SerializeField] private float startTimeOfDay = 0.5f; // Noon

        [Tooltip("Directional light acting as the sun. Auto-found if null.")]
        [SerializeField] private Light sunLight;

        [Header("Sun")]
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Ambient & Fog")]
        [SerializeField] private Gradient ambientColorGradient;
        [SerializeField] private Gradient fogColorGradient;
        [SerializeField] private AnimationCurve fogDensityCurve = AnimationCurve.Linear(0f, 0.001f, 1f, 0.005f);
        [SerializeField] private bool enableFog = true;

        [Header("Weather")]
        [Tooltip("Min duration a weather state lasts (seconds)")]
        [SerializeField] private float minWeatherDuration = 120f;
        [Tooltip("Max duration a weather state lasts (seconds)")]
        [SerializeField] private float maxWeatherDuration = 300f;
        [Tooltip("How fast weather intensity transitions (per second)")]
        [SerializeField] private float weatherTransitionSpeed = 0.3f;

        [Header("Rain Particles")]
        [Tooltip("Assign a ParticleSystem for rain. Emission is controlled by script.")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private float rainEmissionRate = 500f;
        [SerializeField] private float stormEmissionRate = 2000f;

        [Header("Storm / Lightning")]
        [SerializeField] private float minLightningInterval = 5f;
        [SerializeField] private float maxLightningInterval = 15f;
        [SerializeField] private float lightningFlashDuration = 0.15f;
        [SerializeField] private float lightningIntensityBoost = 3f;

        [Header("Weather Modifiers")]
        [Tooltip("How much each weather state dims sun intensity (multiplier)")]
        [SerializeField] private float cloudySunDim = 0.7f;
        [SerializeField] private float rainSunDim = 0.5f;
        [SerializeField] private float stormSunDim = 0.3f;
        [Tooltip("Fog density multiplier per weather state")]
        [SerializeField] private float cloudyFogMult = 1.5f;
        [SerializeField] private float rainFogMult = 2.5f;
        [SerializeField] private float stormFogMult = 4f;

        #endregion

        #region Network State

        private NetworkVariable<float> networkTimeOfDay = new NetworkVariable<float>(
            0.5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<int> networkWeatherState = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<float> networkWeatherIntensity = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Server State

        private float serverTimeOfDay;
        private WeatherState serverWeatherState = WeatherState.Clear;
        private float serverWeatherIntensity;
        private float serverTargetIntensity;
        private float weatherTimer;
        private Coroutine lightningCoroutine;

        #endregion

        #region Client State

        private float currentLightningBoost;
        private ParticleSystem.EmissionModule rainEmission;
        private bool rainInitialized;

        #endregion

        #region Events

        /// <summary>
        /// Fired on all clients when weather state changes. Hook audio, UI, etc.
        /// </summary>
        public event Action<WeatherState> OnWeatherChanged;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaults();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitializeDefaults()
        {
            // Auto-find sun if not assigned
            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            // Build default gradients if not configured in inspector
            if (sunColorGradient == null) sunColorGradient = BuildDefaultSunGradient();
            if (ambientColorGradient == null) ambientColorGradient = BuildDefaultAmbientGradient();
            if (fogColorGradient == null) fogColorGradient = BuildDefaultFogGradient();

            // Initialize rain emission reference
            if (rainParticles != null)
            {
                rainEmission = rainParticles.emission;
                rainEmission.rateOverTime = 0f;
                rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rainInitialized = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                LoadAndApplyAsync();
                SaveGameManager.OnAutoSave += SaveEnvironmentAuto;
            }

            // Listen for weather state changes
            networkWeatherState.OnValueChanged += OnWeatherStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SaveGameManager.OnAutoSave -= SaveEnvironmentAuto;
                SaveEnvironment();
            }

            networkWeatherState.OnValueChanged -= OnWeatherStateChanged;

            if (lightningCoroutine != null)
                StopCoroutine(lightningCoroutine);

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                UpdateServerTime();
                UpdateServerWeather();
            }

            // All clients (including host) apply visuals
            ApplyDayNightVisuals();
            ApplyWeatherVisuals();
            UpdateRainFollow();
        }

        #endregion

        #region Server — Time of Day

        private void UpdateServerTime()
        {
            serverTimeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (serverTimeOfDay >= 1f) serverTimeOfDay -= 1f;

            networkTimeOfDay.Value = serverTimeOfDay;
        }

        #endregion

        #region Server — Weather

        private void UpdateServerWeather()
        {
            // Countdown to next weather change
            weatherTimer -= Time.deltaTime;
            if (weatherTimer <= 0f)
            {
                PickNextWeather();
                weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);
            }

            // Smooth transition of intensity toward target
            serverWeatherIntensity = Mathf.MoveTowards(
                serverWeatherIntensity, serverTargetIntensity, weatherTransitionSpeed * Time.deltaTime);

            networkWeatherIntensity.Value = serverWeatherIntensity;
        }

        private void PickNextWeather()
        {
            // Weighted random: Clear 40%, Cloudy 30%, Rain 20%, Storm 10%
            float roll = UnityEngine.Random.value;
            WeatherState newState;

            if (roll < 0.4f) newState = WeatherState.Clear;
            else if (roll < 0.7f) newState = WeatherState.Cloudy;
            else if (roll < 0.9f) newState = WeatherState.Rain;
            else newState = WeatherState.Storm;

            // Avoid picking same state
            if (newState == serverWeatherState)
                newState = WeatherState.Clear;

            // When transitioning away, fade out first, then switch
            if (serverWeatherIntensity > 0.1f && newState != WeatherState.Clear)
            {
                // Already have weather active — transition through clear briefly
                serverTargetIntensity = 0f;
            }

            serverWeatherState = newState;
            networkWeatherState.Value = (int)newState;

            if (newState == WeatherState.Clear)
                serverTargetIntensity = 0f;
            else
                serverTargetIntensity = 1f;
        }

        #endregion

        #region Client — Day/Night Visuals

        private void ApplyDayNightVisuals()
        {
            float t = networkTimeOfDay.Value;

            if (sunLight != null)
            {
                // Rotate sun: 0 = below horizon (midnight), 0.25 = horizon (sunrise),
                // 0.5 = zenith (noon), 0.75 = horizon (sunset)
                float sunAngle = (t * 360f) - 90f;
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

                // Base color and intensity from gradients
                float baseIntensity = sunIntensityCurve.Evaluate(t);
                Color baseColor = sunColorGradient.Evaluate(t);

                // Apply weather dimming
                float weatherDim = GetWeatherSunDimming();
                sunLight.intensity = baseIntensity * weatherDim + currentLightningBoost;
                sunLight.color = baseColor;
            }

            // Ambient light
            Color baseAmbient = ambientColorGradient.Evaluate(t);
            float ambientWeatherMult = GetWeatherAmbientMult();
            RenderSettings.ambientLight = baseAmbient * ambientWeatherMult;

            // Fog
            if (enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = fogColorGradient.Evaluate(t);
                float baseFogDensity = fogDensityCurve.Evaluate(t);
                float fogWeatherMult = GetWeatherFogMultiplier();
                RenderSettings.fogDensity = baseFogDensity * fogWeatherMult;
            }
        }

        #endregion

        #region Client — Weather Visuals

        private void ApplyWeatherVisuals()
        {
            if (!rainInitialized) return;

            float intensity = networkWeatherIntensity.Value;
            WeatherState weather = (WeatherState)networkWeatherState.Value;

            bool shouldRain = (weather == WeatherState.Rain || weather == WeatherState.Storm) && intensity > 0.05f;

            if (shouldRain)
            {
                if (!rainParticles.isPlaying)
                    rainParticles.Play();

                float targetRate = weather == WeatherState.Storm
                    ? stormEmissionRate * intensity
                    : rainEmissionRate * intensity;

                rainEmission.rateOverTime = targetRate;
            }
            else
            {
                if (intensity < 0.05f && rainParticles.isPlaying)
                {
                    rainEmission.rateOverTime = 0f;
                    rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // Decay lightning boost
            currentLightningBoost = Mathf.MoveTowards(currentLightningBoost, 0f, Time.deltaTime * 20f);
        }

        private void UpdateRainFollow()
        {
            if (rainParticles == null || Camera.main == null) return;

            Vector3 camPos = Camera.main.transform.position;
            rainParticles.transform.position = camPos + Vector3.up * 20f;
        }

        private void OnWeatherStateChanged(int previous, int current)
        {
            WeatherState newState = (WeatherState)current;
            OnWeatherChanged?.Invoke(newState);

            // Manage lightning coroutine
            if (newState == WeatherState.Storm)
            {
                if (lightningCoroutine == null)
                    lightningCoroutine = StartCoroutine(LightningRoutine());
            }
            else
            {
                if (lightningCoroutine != null)
                {
                    StopCoroutine(lightningCoroutine);
                    lightningCoroutine = null;
                }
                currentLightningBoost = 0f;
            }
        }

        #endregion

        #region Weather Modifier Helpers

        private float GetWeatherSunDimming()
        {
            float intensity = networkWeatherIntensity.Value;
            WeatherState weather = (WeatherState)networkWeatherState.Value;

            return weather switch
            {
                WeatherState.Cloudy => Mathf.Lerp(1f, cloudySunDim, intensity),
                WeatherState.Rain => Mathf.Lerp(1f, rainSunDim, intensity),
                WeatherState.Storm => Mathf.Lerp(1f, stormSunDim, intensity),
                _ => 1f
            };
        }

        private float GetWeatherAmbientMult()
        {
            float intensity = networkWeatherIntensity.Value;
            WeatherState weather = (WeatherState)networkWeatherState.Value;

            return weather switch
            {
                WeatherState.Cloudy => Mathf.Lerp(1f, 0.8f, intensity),
                WeatherState.Rain => Mathf.Lerp(1f, 0.6f, intensity),
                WeatherState.Storm => Mathf.Lerp(1f, 0.4f, intensity),
                _ => 1f
            };
        }

        private float GetWeatherFogMultiplier()
        {
            float intensity = networkWeatherIntensity.Value;
            WeatherState weather = (WeatherState)networkWeatherState.Value;

            return weather switch
            {
                WeatherState.Cloudy => Mathf.Lerp(1f, cloudyFogMult, intensity),
                WeatherState.Rain => Mathf.Lerp(1f, rainFogMult, intensity),
                WeatherState.Storm => Mathf.Lerp(1f, stormFogMult, intensity),
                _ => 1f
            };
        }

        #endregion

        #region Lightning

        private IEnumerator LightningRoutine()
        {
            while (true)
            {
                float wait = UnityEngine.Random.Range(minLightningInterval, maxLightningInterval);
                yield return new WaitForSeconds(wait);

                if ((WeatherState)networkWeatherState.Value == WeatherState.Storm)
                {
                    if (IsServer)
                        LightningFlashRpc();
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void LightningFlashRpc()
        {
            StartCoroutine(DoLightningFlash());
        }

        private IEnumerator DoLightningFlash()
        {
            currentLightningBoost = lightningIntensityBoost;
            yield return new WaitForSeconds(lightningFlashDuration);
            // Boost decays in ApplyWeatherVisuals via MoveTowards
        }

        #endregion

        #region Save / Load

        [Serializable]
        private class EnvironmentSaveData
        {
            public float timeOfDay;
            public int weatherState;
            public float weatherIntensity;
            public long timestamp;
        }

        private void SaveEnvironmentAuto()
        {
            if (IsServer) SaveEnvironment();
        }

        private async void SaveEnvironment()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId)) return;

            try
            {
                var data = new EnvironmentSaveData
                {
                    timeOfDay = serverTimeOfDay,
                    weatherState = (int)serverWeatherState,
                    weatherIntensity = serverWeatherIntensity,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await SaveGameManager.Instance.SaveGameDataAsync(gameId, "environment", data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentManager] Save failed: {e.Message}");
            }
        }

        private async void LoadAndApplyAsync()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                // No save — use defaults
                ApplyDefaults();
                return;
            }

            try
            {
                var data = await SaveGameManager.Instance.LoadGameDataAsync<EnvironmentSaveData>(
                    gameId, "environment");

                if (data != null && data.timestamp > 0)
                {
                    serverTimeOfDay = data.timeOfDay;
                    serverWeatherState = (WeatherState)data.weatherState;
                    serverWeatherIntensity = data.weatherIntensity;
                    serverTargetIntensity = serverWeatherIntensity;

                    networkTimeOfDay.Value = serverTimeOfDay;
                    networkWeatherState.Value = data.weatherState;
                    networkWeatherIntensity.Value = data.weatherIntensity;

                    weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);

                    Debug.Log($"[EnvironmentManager] Loaded: time={serverTimeOfDay:F2}, weather={serverWeatherState}");
                }
                else
                {
                    ApplyDefaults();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentManager] Load failed: {e.Message}");
                ApplyDefaults();
            }
        }

        private void ApplyDefaults()
        {
            serverTimeOfDay = startTimeOfDay;
            serverWeatherState = WeatherState.Clear;
            serverWeatherIntensity = 0f;
            serverTargetIntensity = 0f;

            networkTimeOfDay.Value = startTimeOfDay;
            networkWeatherState.Value = 0;
            networkWeatherIntensity.Value = 0f;

            weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);
        }

        #endregion

        #region Public API

        /// <summary>Current time of day (0=midnight, 0.5=noon). Read from any client.</summary>
        public float TimeOfDay => networkTimeOfDay.Value;

        /// <summary>Current weather state. Read from any client.</summary>
        public WeatherState CurrentWeather => (WeatherState)networkWeatherState.Value;

        /// <summary>Current weather intensity (0-1). Read from any client.</summary>
        public float WeatherIntensity => networkWeatherIntensity.Value;

        /// <summary>True if it's nighttime (sun below horizon).</summary>
        public bool IsNight => networkTimeOfDay.Value < 0.25f || networkTimeOfDay.Value > 0.75f;

        /// <summary>Server only: Force a specific weather state.</summary>
        public void SetWeather(WeatherState state)
        {
            if (!IsServer) return;
            serverWeatherState = state;
            networkWeatherState.Value = (int)state;
            serverTargetIntensity = state == WeatherState.Clear ? 0f : 1f;
        }

        /// <summary>Server only: Set time of day directly (0-1).</summary>
        public void SetTimeOfDay(float time)
        {
            if (!IsServer) return;
            serverTimeOfDay = Mathf.Repeat(time, 1f);
            networkTimeOfDay.Value = serverTimeOfDay;
        }

        #endregion

        #region Default Gradients

        private Gradient BuildDefaultSunGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new(new Color(0.1f, 0.1f, 0.2f), 0f),      // Midnight — deep blue
                    new(new Color(0.9f, 0.5f, 0.3f), 0.23f),    // Pre-sunrise — warm orange
                    new(new Color(1f, 0.85f, 0.7f), 0.27f),     // Sunrise — golden
                    new(new Color(1f, 0.97f, 0.92f), 0.5f),     // Noon — warm white
                    new(new Color(1f, 0.7f, 0.4f), 0.73f),      // Sunset — orange
                    new(new Color(0.8f, 0.3f, 0.2f), 0.77f),    // Post-sunset — deep red
                    new(new Color(0.1f, 0.1f, 0.2f), 1f),       // Midnight
                },
                new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
            );
            return g;
        }

        private Gradient BuildDefaultAmbientGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new(new Color(0.05f, 0.05f, 0.1f), 0f),     // Midnight
                    new(new Color(0.4f, 0.35f, 0.3f), 0.25f),   // Sunrise
                    new(new Color(0.7f, 0.75f, 0.8f), 0.5f),    // Noon
                    new(new Color(0.5f, 0.35f, 0.25f), 0.75f),  // Sunset
                    new(new Color(0.05f, 0.05f, 0.1f), 1f),     // Midnight
                },
                new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
            );
            return g;
        }

        private Gradient BuildDefaultFogGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new(new Color(0.05f, 0.05f, 0.08f), 0f),    // Midnight
                    new(new Color(0.6f, 0.5f, 0.4f), 0.25f),    // Sunrise
                    new(new Color(0.75f, 0.8f, 0.85f), 0.5f),   // Noon
                    new(new Color(0.6f, 0.4f, 0.3f), 0.75f),    // Sunset
                    new(new Color(0.05f, 0.05f, 0.08f), 1f),    // Midnight
                },
                new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
            );
            return g;
        }

        #endregion
    }
}