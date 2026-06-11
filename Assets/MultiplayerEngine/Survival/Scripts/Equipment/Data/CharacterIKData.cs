using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// ScriptableObject that stores embedded IK presets for weapons and global support hand offsets.
    ///
    /// Shooter IK offsets are managed via these embedded presets (per-character,
    /// per-weapon-category).
    /// Hand offsets are managed solely within the InventoryItemData itself.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterIKData", menuName = "Multiplayer Engine/Equipment/Character IK Data", order = 11)]
    public class CharacterIKData : ScriptableObject
    {
        [Header("Weapon IK Presets")]
        [Tooltip("List of per-weapon-category IK presets")]
        public List<WeaponIKPreset> presets = new List<WeaponIKPreset>();


        /// <summary>
        /// Finds the IK preset assigned to the given InventoryItemData.
        /// Returns null if no preset is found.
        /// </summary>
        public WeaponIKPreset GetPreset(InventoryItemData itemData)
        {
            if (itemData == null) return null;

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == null) continue;
                if (presets[i].itemData == itemData)
                    return presets[i];
            }
            return null;
        }

        /// <summary>
        /// Returns the index of the given preset in the list (-1 if not found).
        /// </summary>
        public int IndexOfPreset(WeaponIKPreset preset)
        {
            return presets.IndexOf(preset);
        }
    }
}
