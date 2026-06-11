using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Attach to any GameObject you want a UI marker for.
    /// Purely data — no per-frame work. Registers itself with MarkerManager.
    /// </summary>
    public class MarkerTarget : MonoBehaviour
    {
        [Header("Marker Settings")]
        [Tooltip("Icon displayed in the marker")]
        [SerializeField] private Sprite icon;

        [Tooltip("Name shown at close range")]
        [SerializeField] private string displayName = "Target";

        [Tooltip("Tint color for icon and text")]
        [SerializeField] private Color markerColor = Color.white;

        [Tooltip("Vertical offset above the target's pivot (world units)")]
        [SerializeField] private float heightOffset = 2f;

        [Tooltip("Show this marker by default when enabled")]
        [SerializeField] private bool showByDefault = true;

        /// <summary>World position used for marker tracking (pivot + height offset).</summary>
        public Vector3 MarkerWorldPosition => transform.position + Vector3.up * heightOffset;

        public Sprite Icon => icon;
        public string DisplayName => displayName;
        public Color MarkerColor => markerColor;
        public bool ShowByDefault => showByDefault;

        /// <summary>
        /// Optional lifetime in seconds. When set (>0), MarkerManager will
        /// auto-remove this marker after the duration expires.
        /// -1 means permanent (no auto-expire).
        /// </summary>
        public float Lifetime { get; set; } = -1f;

        /// <summary>
        /// Whether this marker was created as a temporary ping
        /// and should have its GameObject destroyed on expire.
        /// </summary>
        public bool DestroyOnExpire { get; set; } = false;

        /// <summary>
        /// Health percentage (0.0 to 1.0) for displaying a health bar.
        /// -1 means no health bar should be shown.
        /// </summary>
        public float HealthPercent { get; set; } = -1f;

        private void OnEnable()
        {
            if (showByDefault)
                MarkerManager.RegisterPending(this);
        }

        private void OnDisable()
        {
            if (MarkerManager.Instance != null)
                MarkerManager.Instance.Unregister(this);
        }

        /// <summary>Manual show/hide for gameplay logic (quests, highlights, etc.).</summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (MarkerManager.Instance != null)
                    MarkerManager.Instance.Register(this);
            }
            else
            {
                if (MarkerManager.Instance != null)
                    MarkerManager.Instance.Unregister(this);
            }
        }

        /// <summary>
        /// Configure this marker at runtime (used by ping system).
        /// </summary>
        public void SetPingData(string name, Sprite pingIcon, Color color, float heightOff = 2f)
        {
            displayName = name;
            icon = pingIcon;
            markerColor = color;
            heightOffset = heightOff;
        }

        /// <summary>
        /// Creates a temporary ping marker at the given world position.
        /// The created GameObject is parented to the target transform so it follows a moving target.
        /// If target is null, creates a static marker at worldPos.
        /// </summary>
        /// <param name="target">Transform to follow (can be null for static positions).</param>
        /// <param name="worldPos">Initial world position (used only if target is null).</param>
        /// <param name="name">Display name for the marker.</param>
        /// <param name="pingIcon">Icon sprite (can be null).</param>
        /// <param name="color">Marker tint color.</param>
        /// <param name="lifetime">Auto-expire time in seconds.</param>
        /// <returns>The created MarkerTarget.</returns>
        public static MarkerTarget CreatePing(Transform target, Vector3 worldPos, string name,
                                               Sprite pingIcon, Color color, float lifetime = 30f,
                                               float heightOffset = 2f)
        {
            var go = new GameObject($"Ping_{name}");
            go.SetActive(false); // Prevent OnEnable from firing during setup

            if (target != null)
            {
                // Parent to the target so the marker follows it
                go.transform.SetParent(target, false);
                go.transform.localPosition = Vector3.zero;
            }
            else
            {
                // Static world position
                go.transform.position = worldPos;
            }

            var marker = go.AddComponent<MarkerTarget>();
            marker.SetPingData(name, pingIcon, color, heightOffset);
            marker.showByDefault = false; // Don't auto-register in OnEnable
            marker.Lifetime = lifetime;
            marker.DestroyOnExpire = true;

            go.SetActive(true); // OnEnable fires now but showByDefault is false, so no auto-register

            // Register manually (Lifetime is already set, so the timer will be created)
            if (MarkerManager.Instance != null)
                MarkerManager.Instance.Register(marker);
            else
                MarkerManager.RegisterPending(marker);

            return marker;
        }
    }
}