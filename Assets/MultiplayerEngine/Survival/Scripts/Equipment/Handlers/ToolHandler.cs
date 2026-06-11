using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for tools (axe, pickaxe).
    /// Tools are used exclusively for harvesting resources.
    /// Uses BladeHitbox with BladeEnable/BladeDisable animation events for hit detection.
    /// </summary>
    public class ToolHandler : BaseItemHandler
    {
        private ToolData toolData;
        private IStaminaManager staminaManager;
        private float lastUseTime;
        
        // Blade-based hit detection
        private BladeHitbox bladeHitbox;



        protected override void OnEquipInternal()
        {
            toolData = itemData.toolData;
            staminaManager = controller.GetStaminaManager();

            // Pre-set ItemID so click only needs to trigger IsAction
            SetActionParameters();
        }

        protected override void OnUnequipInternal()
        {
            // Ensure blade detection is stopped
            OnBladeDisable();
            bladeHitbox = null;
        }

        /// <summary>
        /// Called after the visual model is spawned.
        /// Finds BladeHitbox component on the equipped object.
        /// </summary>
        protected override void OnVisualModelSpawned()
        {
            base.OnVisualModelSpawned();
            
            if (equippedObject != null)
            {
                bladeHitbox = equippedObject.GetComponentInChildren<BladeHitbox>();
            }
        }

        public override void OnPrimaryAction(bool pressed)
        {
            if (!pressed) return;
            if (!CanUse()) return;

            UseTool();
        }

        private bool CanUse()
        {
            // Check cooldown
            float cooldown = toolData?.useCooldown ?? 0.5f;
            if (Time.time < lastUseTime + cooldown)
                return false;

            // Check stamina
            float staminaCost = toolData?.staminaCost ?? 8f;
            if (staminaManager != null && !staminaManager.HasStamina(staminaCost))
                return false;

            return true;
        }

        private void UseTool()
        {
            lastUseTime = Time.time;

            // Consume stamina
            float staminaCost = toolData?.staminaCost ?? 8f;
            staminaManager?.TryUseStamina(staminaCost);

            // Trigger action animation - ItemID already set on equip, just trigger IsAction
            TriggerAction();

            // Sync animation to other players
            if (controller != null && controller.IsOwner)
                controller.SyncPrimaryActionRpc(true);

            // Reset blade hitbox tracking for new swing
            bladeHitbox?.ResetHitTracking();
        }

        #region Blade Animation Events

        /// <summary>
        /// Called by BladeEnable animation event.
        /// Starts continuous blade hit detection.
        /// </summary>
        public override void OnBladeEnable()
        {
            if (bladeHitbox != null)
            {
                bladeHitbox.StartContinuousDetection();
                bladeHitbox.OnHitDetected -= OnBladeHit;
                bladeHitbox.OnHitDetected += OnBladeHit;
            }
        }

        /// <summary>
        /// Called by BladeDisable animation event.
        /// Stops continuous blade hit detection.
        /// </summary>
        public override void OnBladeDisable()
        {
            if (bladeHitbox != null)
            {
                bladeHitbox.StopContinuousDetection();
                bladeHitbox.OnHitDetected -= OnBladeHit;
            }

            // Action complete - reset action layer weight if it's a separate layer
            EndAction();
        }

        /// <summary>
        /// Callback when blade detects a hit during continuous detection.
        /// </summary>
        private void OnBladeHit(RaycastHit hit)
        {
            ProcessHit(hit);
        }

        #endregion

        #region Hit Processing

        private void ProcessHit(RaycastHit hit)
        {
            Vector3 hitPoint = hit.point;

            // Check for harvestable resources (LocalResource system)
            var harvestable = hit.collider.GetComponentInParent<IHarvestable>();
            if (harvestable != null && harvestable.IsAlive)
            {
                if (toolData.CanHarvest(harvestable.ResourceType) && toolData.MeetsTierRequirement(harvestable.RequiredTier))
                {
                    // LocalResource routes through ResourceManager internally
                    harvestable.TakeHarvestDamage(toolData.harvestDamage, hitPoint, controller.NetworkObjectId);
                    ReduceDurability(1);
                }
            }
        }

        #endregion
    }
}