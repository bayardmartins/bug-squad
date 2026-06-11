using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// ScriptableObject containing melee weapon configuration.
    /// Melee weapons use combo-based attacks with blade hitbox detection.
    /// </summary>
    // [CreateAssetMenu] removed as this is created via ItemDatabaseWindow
    public class MeleeWeaponData : ScriptableObject
    {
        [Header("Combo Settings")]
        [Tooltip("Animator layer index used for combo attack animations")]
        public int comboLayerIndex = 1;

        [Tooltip("Enable root motion during combo attacks (animation drives movement)")]
        public bool useRootMotion = false;

        [Tooltip("Maximum number of attacks in the combo chain")]
        public int maxComboCount = 3;

        [Header("Attack Stats")]
        [Tooltip("Base damage per hit")]
        public float baseDamage = 10f;

        [Tooltip("Damage multiplier per combo hit (index 0 = first attack). If empty, all hits use baseDamage.")]
        public float[] comboDamageMultipliers;

        [Header("Usage")]
        [Tooltip("Stamina cost per attack")]
        public float staminaCostPerAttack = 10f;

        /// <summary>
        /// Gets the damage for a specific combo index.
        /// Falls back to baseDamage if no multiplier is defined.
        /// </summary>
        public float GetDamageForCombo(int comboIndex)
        {
            if (comboDamageMultipliers != null && comboIndex < comboDamageMultipliers.Length)
                return baseDamage * comboDamageMultipliers[comboIndex];
            return baseDamage;
        }
    }
}
