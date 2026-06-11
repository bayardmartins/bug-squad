using UnityEngine;

namespace Ignitives.MultiplayerEngine 
{
    public class LoadingIcon : MonoBehaviour
    {
        [SerializeField] private float interval = 0.1f;
        [SerializeField] private int totalSteps = 8;

        private float timer = 0f;
        private int currentStep = 0;

        private void Update()
        {
            timer += Time.deltaTime;

            if (timer >= interval)
            {
                timer = 0f;
                currentStep = (currentStep + 1) % totalSteps;
                float angle = currentStep * 45f; // 0, 45, 90, ..., 315
                transform.rotation = Quaternion.Euler(0, 0, -angle); // Clockwise localRotation
            }
        }
    }
}
