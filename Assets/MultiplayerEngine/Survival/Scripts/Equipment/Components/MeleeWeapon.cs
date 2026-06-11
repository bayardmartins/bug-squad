using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Attached to melee weapon visual prefabs to configure hit effects and sounds.
    /// Handled by MeleeWeaponHandler to play effects on hit and swing.
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("Effects")]
        [Tooltip("Particle effect prefab to spawn on hit")]
        public GameObject hitEffectPrefab;

        [Tooltip("Sound to play on swing")]
        public AudioClip swingSound;

        [Tooltip("Sound to play on hit")]
        public AudioClip hitSound;
    }
}