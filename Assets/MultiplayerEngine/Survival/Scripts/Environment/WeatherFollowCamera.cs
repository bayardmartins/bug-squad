using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Keeps a rain/weather particle system centered above the local player's camera.
    /// Attach this to the same GameObject as your rain ParticleSystem.
    /// Only needed if you want the rain to follow a specific offset rather than
    /// the default camera-follow built into EnvironmentManager.
    /// </summary>
    public class WeatherFollowCamera : MonoBehaviour
    {
        [Tooltip("Height offset above the camera position")]
        [SerializeField] private float heightOffset = 20f;

        [Tooltip("If true, only follows XZ (keeps fixed Y height above camera)")]
        [SerializeField] private bool lockY = true;

        private Transform cameraTransform;

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                if (Camera.main != null)
                    cameraTransform = Camera.main.transform;
                else
                    return;
            }

            Vector3 targetPos = cameraTransform.position;

            if (lockY)
                targetPos.y = cameraTransform.position.y + heightOffset;
            else
                targetPos += Vector3.up * heightOffset;

            transform.position = targetPos;
        }
    }
}