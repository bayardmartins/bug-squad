using UnityEngine;
using System.Collections;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Optional component for build piece prefabs that enables physics-based collapse.
    /// When structural stability drops below the threshold, this converts the piece
    /// into a falling rigidbody with break effects, then fades and destroys the debris.
    /// 
    /// If this component is NOT present on a piece, it will be destroyed instantly
    /// (backward compatible with existing prefabs).
    /// </summary>
    public class BuildPhysicalCollapse : MonoBehaviour
    {
        [Header("Break Effects")]
        [Tooltip("Particle system prefab spawned at the piece's position when it collapses.")]
        public GameObject breakParticles;

        [Tooltip("Sound played when the piece breaks apart.")]
        public AudioClip breakSound;

        [Header("Physics Settings")]
        [Tooltip("Force applied to the piece when it starts falling. Higher = more dramatic.")]
        public float explosionForce = 2f;

        [Tooltip("Random torque applied for tumbling effect.")]
        public float tumbleForce = 1.5f;

        [Tooltip("Mass of the rigidbody during collapse.")]
        public float collapseMass = 15f;

        [Header("Cleanup")]
        [Tooltip("Seconds to wait before starting the fade-out effect.")]
        public float fadeDelay = 3f;

        [Tooltip("Duration of the fade-out animation in seconds.")]
        public float fadeDuration = 1.5f;

        private bool hasCollapsed = false;

        /// <summary>
        /// Triggers the physical collapse sequence:
        /// 1. Spawn break particles and play sound
        /// 2. Switch layer to prevent building on debris
        /// 3. Add rigidbody with gravity and random forces
        /// 4. Fade out and destroy after delay
        /// </summary>
        public void TriggerCollapse()
        {
            if (hasCollapsed) return;
            hasCollapsed = true;

            // 1. Spawn break particles
            if (breakParticles != null)
            {
                Instantiate(breakParticles, transform.position, transform.rotation);
            }

            // 2. Play break sound
            if (breakSound != null)
            {
                AudioSource.PlayClipAtPoint(breakSound, transform.position);
            }

            // 3. Remove build layers to prevent building on falling debris
            SetLayerRecursive(gameObject, LayerMask.NameToLayer("Ignore Raycast"));

            // 4. Unregister snap points so they don't interfere with live building
            var snapPoints = GetComponentsInChildren<SnapPoint>();
            foreach (var sp in snapPoints)
            {
                if (SnapPointRegistry.Instance != null)
                    SnapPointRegistry.Instance.Unregister(sp);
            }

            // 5. Enable physics
            // Disable all existing colliders first, then add a simple one for debris
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                col.enabled = true;
                col.isTrigger = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = collapseMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Apply random fall nudge for cinematic effect
            Vector3 randomNudge = new Vector3(
                Random.Range(-0.5f, 0.5f),
                -0.2f,
                Random.Range(-0.5f, 0.5f)
            );
            rb.AddForce(randomNudge * explosionForce, ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * tumbleForce, ForceMode.Impulse);

            // 6. Start fade and destroy coroutine
            StartCoroutine(FadeAndDestroyDebris());
        }

        private IEnumerator FadeAndDestroyDebris()
        {
            yield return new WaitForSeconds(fadeDelay);

            float elapsed = 0f;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            // Prepare materials for transparency
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                {
                    // Switch to transparent rendering mode for URP Lit
                    if (mat.HasProperty("_Surface"))
                    {
                        mat.SetFloat("_Surface", 1); // 1 = Transparent
                        mat.SetFloat("_Blend", 0);    // 0 = Alpha
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                }
            }

            // Fade out alpha
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / fadeDuration);

                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    foreach (var mat in rend.materials)
                    {
                        // Try URP _BaseColor first, fall back to _Color
                        if (mat.HasProperty("_BaseColor"))
                        {
                            Color col = mat.GetColor("_BaseColor");
                            col.a = alpha;
                            mat.SetColor("_BaseColor", col);
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            Color col = mat.color;
                            col.a = alpha;
                            mat.color = col;
                        }
                    }
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
