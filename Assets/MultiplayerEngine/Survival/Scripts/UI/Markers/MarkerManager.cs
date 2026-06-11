using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Singleton that drives all marker UI elements.
    /// Place on the HUD Canvas. Assign a MarkerUI prefab.
    /// </summary>
    public class MarkerManager : MonoBehaviour
    {
        public static MarkerManager Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("MarkerUI prefab to instantiate for each tracked target")]
        [SerializeField] private MarkerUI markerPrefab;

        [Header("Distance Thresholds")]
        [Tooltip("Below this distance: show Icon + Name")]
        [SerializeField] private float closeRange = 50f;
        [Tooltip("Below this distance (and above close): show Icon + Distance. Above this: Icon only")]
        [SerializeField] private float farRange = 1000f;

        [Header("Off-Screen")]
        [Tooltip("Padding from screen edges in pixels")]
        [SerializeField] private float edgePadding = 50f;

        [Header("Ping Settings")]
        [Tooltip("Default icon used for pings when no item icon is available")]
        [SerializeField] private Sprite defaultPingIcon;

        [Tooltip("Duration in seconds before the ping marker expires")]
        [SerializeField] private float pingLifetime = 30f;

        [Tooltip("Cooldown between pings in seconds")]
        [SerializeField] private float pingCooldown = 1f;

        [Tooltip("Vertical offset above the target's pivot for ping markers (world units)")]
        [SerializeField] private float pingHeightOffset = 2f;

        [Header("References")]
        [Tooltip("The Canvas RectTransform (auto-detected if left empty)")]
        [SerializeField] private RectTransform canvasRect;

        // ── Internal state ──
        private Camera mainCamera;
        private Canvas parentCanvas;
        private Camera canvasCamera; // null for Overlay, the render cam for Camera/WorldSpace
        private readonly List<MarkerTarget> activeTargets = new List<MarkerTarget>();
        private readonly Dictionary<MarkerTarget, MarkerUI> targetToUI = new Dictionary<MarkerTarget, MarkerUI>();
        private readonly Queue<MarkerUI> pool = new Queue<MarkerUI>();

        // ── Lifetime tracking ──
        private readonly Dictionary<MarkerTarget, float> lifetimeTimers = new Dictionary<MarkerTarget, float>();

        // ── Pending registrations (handles race condition) ──
        private static readonly List<MarkerTarget> pendingRegistrations = new List<MarkerTarget>();

        // ── Public accessors for ping settings ──
        public Sprite DefaultPingIcon => defaultPingIcon;
        public float PingLifetime => pingLifetime;
        public float PingCooldown => pingCooldown;

        // ───────────────────────── Lifecycle ─────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (canvasRect == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                    canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
            else
            {
                parentCanvas = canvasRect.GetComponent<Canvas>();
            }

            // Cache the canvas camera for ScreenPointToLocalPointInRectangle
            if (parentCanvas != null)
            {
                // For Overlay mode, camera must be null; for Camera/World mode use the render camera
                canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : parentCanvas.worldCamera;
            }
        }

        private void OnEnable()
        {
            // Flush any targets that tried to register before Instance existed
            if (pendingRegistrations.Count > 0)
            {
                for (int i = 0; i < pendingRegistrations.Count; i++)
                {
                    MarkerTarget pending = pendingRegistrations[i];
                    if (pending != null)
                        Register(pending);
                }
                pendingRegistrations.Clear();
            }
        }

        private void LateUpdate()
        {
            // Cache or refresh camera (same pattern used across the project)
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null || canvasRect == null) return;

            float dt = Time.deltaTime;

            // Update each active marker
            for (int i = activeTargets.Count - 1; i >= 0; i--)
            {
                MarkerTarget target = activeTargets[i];

                // Unity fake-null: destroyed targets compare == null
                if (target == null)
                {
                    activeTargets.RemoveAt(i);
                    // Can't look up in dictionary with destroyed key — handled below
                    continue;
                }

                // ── Lifetime expiration ──
                if (target.Lifetime > 0f && lifetimeTimers.ContainsKey(target))
                {
                    lifetimeTimers[target] -= dt;
                    if (lifetimeTimers[target] <= 0f)
                    {
                        ExpireTarget(target, i);
                        continue;
                    }
                }

                // Get or create UI for this target
                if (!targetToUI.TryGetValue(target, out MarkerUI ui))
                {
                    ui = GetFromPool();
                    if (ui == null) continue; // prefab not assigned
                    targetToUI[target] = ui;
                }

                ui.UpdateMarker(target, mainCamera, canvasRect, canvasCamera,
                                closeRange, farRange, edgePadding);
            }

            // Cleanup: remove dictionary entries whose key was destroyed
            // (only needed rarely, so we do a lazy pass)
            CleanupDestroyedEntries();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>Register a target to be tracked.</summary>
        public void Register(MarkerTarget target)
        {
            if (target == null || activeTargets.Contains(target)) return;
            activeTargets.Add(target);

            // Start lifetime timer if applicable
            if (target.Lifetime > 0f)
                lifetimeTimers[target] = target.Lifetime;
        }

        /// <summary>
        /// Called by MarkerTarget.OnEnable when Instance may not be ready yet.
        /// </summary>
        public static void RegisterPending(MarkerTarget target)
        {
            if (Instance != null)
            {
                Instance.Register(target);
            }
            else
            {
                if (!pendingRegistrations.Contains(target))
                    pendingRegistrations.Add(target);
            }
        }

        /// <summary>Unregister a target and recycle its UI.</summary>
        public void Unregister(MarkerTarget target)
        {
            if (target == null) return;

            activeTargets.Remove(target);
            lifetimeTimers.Remove(target);

            if (targetToUI.TryGetValue(target, out MarkerUI ui))
            {
                ReturnToPool(ui);
                targetToUI.Remove(target);
            }
        }

        /// <summary>
        /// Creates a temporary ping marker at a target transform's position.
        /// The marker follows the target and auto-expires after pingLifetime seconds.
        /// </summary>
        /// <param name="target">Transform to follow (can be null for a static position).</param>
        /// <param name="worldPos">World position (used only if target is null).</param>
        /// <param name="name">Display name for the marker.</param>
        /// <param name="icon">Icon sprite (falls back to defaultPingIcon if null).</param>
        /// <returns>The created MarkerTarget.</returns>
        public MarkerTarget PingAt(Transform target, Vector3 worldPos, string name, Sprite icon = null)
        {
            Sprite pingIcon = icon != null ? icon : defaultPingIcon;

            return MarkerTarget.CreatePing(target, worldPos, name, pingIcon, Color.white, pingLifetime, pingHeightOffset);
        }

        // ───────────────────────── Lifetime Expiration ─────────────────────────

        private void ExpireTarget(MarkerTarget target, int listIndex)
        {
            // Remove from active list
            activeTargets.RemoveAt(listIndex);
            lifetimeTimers.Remove(target);

            // Recycle UI
            if (targetToUI.TryGetValue(target, out MarkerUI ui))
            {
                ReturnToPool(ui);
                targetToUI.Remove(target);
            }

            // Destroy the temp GameObject if flagged
            if (target.DestroyOnExpire && target.gameObject != null)
            {
                Destroy(target.gameObject);
            }
        }

        // ───────────────────────── Object Pool ─────────────────────────

        private MarkerUI GetFromPool()
        {
            MarkerUI ui;
            if (pool.Count > 0)
            {
                ui = pool.Dequeue();
            }
            else
            {
                if (markerPrefab == null)
                {
                    Debug.LogError("[MarkerManager] MarkerUI prefab is not assigned!", this);
                    return null;
                }
                ui = Instantiate(markerPrefab, canvasRect);
            }

            ui.gameObject.SetActive(true);
            return ui;
        }

        private void ReturnToPool(MarkerUI ui)
        {
            if (ui == null) return;
            ui.ResetMarker();
            pool.Enqueue(ui);
        }

        // ───────────────────────── Cleanup ─────────────────────────

        private readonly List<MarkerTarget> keysToRemove = new List<MarkerTarget>();

        private void CleanupDestroyedEntries()
        {
            keysToRemove.Clear();
            foreach (var kvp in targetToUI)
            {
                if (kvp.Key == null)
                    keysToRemove.Add(kvp.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                if (targetToUI.TryGetValue(keysToRemove[i], out MarkerUI ui))
                    ReturnToPool(ui);
                targetToUI.Remove(keysToRemove[i]);
            }
        }
    }
}