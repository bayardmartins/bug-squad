using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// A simple handler for items that just need to be equipped and visible 
    /// (e.g. Weapons in the new system, or simple props).
    /// </summary>
    public class SimpleEquipHandler : BaseItemHandler
    {


        // Override to set action parameters on equip
        protected override void OnEquipInternal()
        {
            // Pre-set ItemID so click only needs to trigger IsAction
            SetActionParameters();
        }
        
        public override void OnPrimaryAction(bool pressed)
        {
            // Future: Trigger 'Use' animation or similar
            // When implemented, just call TriggerAction() since ItemID already set
        }

        public override void OnSecondaryAction(bool pressed)
        {
            // Future: Trigger 'Aim' or alternate action
        }
    }
}