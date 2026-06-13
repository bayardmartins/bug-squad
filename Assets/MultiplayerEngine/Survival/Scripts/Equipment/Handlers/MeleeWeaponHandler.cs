using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for melee weapons with combo attack system.
    /// Locks movement during attacks. Combo breaks on movement input.
    /// Uses ComboAttackBehavior on animation states for combo window timing.
    /// </summary>
    public class MeleeWeaponHandler : BaseItemHandler
    {
        private MeleeWeaponData meleeData;
        private PlayerController playerController;
        private IStaminaManager staminaManager;
        private BladeHitbox bladeHitbox;

        // Prefab-attached melee components
        private MeleeWeapon meleeWeaponComponent;

        // Combo state
        private int currentComboIndex = 0;
        private bool isAttacking = false;
        private bool comboWindowOpen = false;
        private bool comboQueued = false;
        private bool exitRequested = false; // Movement detected → break combo after current attack

        // Animator parameter hashes
        private static readonly int ComboIndexHash = Animator.StringToHash("ComboIndex");

        #region Equip / Unequip


        protected override void OnEquipInternal()
        {
            meleeData = itemData.meleeWeaponData;
            playerController = controller.GetPlayerController();
            staminaManager = controller.GetStaminaManager();

            SetActionParameters();
        }

        protected override void OnUnequipInternal()
        {
            // Ensure we clean up state on unequip
            if (isAttacking && playerController != null)
            {
                playerController.UnlockMovement();
            }

            // Reset combo layer weight and root motion
            if (meleeData != null)
            {
                var animator = controller?.GetAnimator();
                if (animator != null)
                {
                    animator.SetLayerWeight(meleeData.comboLayerIndex, 0f);
                    if (meleeData.useRootMotion)
                        animator.applyRootMotion = false;
                }
            }

            OnBladeDisable();
            bladeHitbox = null;
            meleeWeaponComponent = null;
            isAttacking = false;
            currentComboIndex = 0;
            comboWindowOpen = false;
            comboQueued = false;
            exitRequested = false;
        }

        protected override void OnVisualModelSpawned()
        {
            base.OnVisualModelSpawned();

            if (equippedObject != null)
            {
                bladeHitbox = equippedObject.GetComponentInChildren<BladeHitbox>();
                meleeWeaponComponent = equippedObject.GetComponent<MeleeWeapon>();
            }
        }

        #endregion

        #region Input & Update

        public override void OnPrimaryAction(bool pressed)
        {
            if (!pressed) return;

            if (!isAttacking)
            {
                // Start first attack
                if (CanAttack())
                    StartAttack();
            }
            else if (comboWindowOpen && !exitRequested)
            {
                // Queue next combo attack
                comboQueued = true;
            }
        }

        public override void OnUpdate()
        {
            // Check for movement input during attack to trigger combo break
            if (isAttacking && playerController != null && playerController.HasMovementInput())
            {
                exitRequested = true;
            }
        }

        #endregion

        #region Attack Logic

        private bool CanAttack()
        {
            // Block attacks if not grounded
            if (playerController != null && !playerController.Grounded)
                return false;

            // Check stamina
            float staminaCost = meleeData?.staminaCostPerAttack ?? 10f;
            if (staminaManager != null && !staminaManager.HasStamina(staminaCost))
                return false;

            return true;
        }

        private void StartAttack()
        {
            // Lock movement
            playerController?.LockMovement();
            isAttacking = true;
            comboQueued = false;

            // Notify EquipmentController that combo layer is now managed by ComboAttackBehavior
            if (meleeData != null)
                controller.OnComboAttackStarted(meleeData.comboLayerIndex);

            // Consume stamina
            float staminaCost = meleeData?.staminaCostPerAttack ?? 10f;
            staminaManager?.TryUseStamina(staminaCost);

            // Set combo index and trigger attack animation
            var animator = controller.GetAnimator();
            if (animator != null)
            {
                animator.SetInteger(ComboIndexHash, currentComboIndex);

                // Enable root motion on first attack if configured
                if (currentComboIndex == 0 && meleeData != null && meleeData.useRootMotion)
                    animator.applyRootMotion = true;
            }
            TriggerAction(force: true);  // Force bypass spam guard for combo chain

            // Play swing sound
            PlaySwingSound();

            // Sync animation to other players
            if (controller != null && controller.IsOwner)
                controller.SyncPrimaryActionRpc(true);

            // Reset blade hitbox tracking for new swing
            bladeHitbox?.ResetHitTracking();
        }

        #endregion

        #region Combo Events (Called by ComboAttackBehavior via EquipmentController)

        public override void OnComboWindowStart()
        {
            comboWindowOpen = true;
        }

        public override void OnComboWindowEnd()
        {
            comboWindowOpen = false;

            int maxCombo = meleeData?.maxComboCount ?? 3;

            if (comboQueued && !exitRequested && currentComboIndex < maxCombo - 1)
            {
                // Continue combo → next attack
                currentComboIndex++;
                comboQueued = false;
                exitRequested = false;

                if (CanAttack())
                    StartAttack();
                else
                    ResetComboState();
            }
            else
            {
                // Combo not continuing — signal that combo attack is ending
                // so ComboAttackBehavior.OnStateExit will clean up (layer weight → 0, root motion off)
                controller?.OnComboAttackEnded();
            }
        }

        public override void OnAttackExitEnd()
        {
            // Attack animation fully exited → unlock movement and reset
            ResetComboState();
        }

        private void ResetComboState()
        {
            isAttacking = false;
            isPerformingAction = false;  // Clear base class action lock
            currentComboIndex = 0;
            comboQueued = false;
            comboWindowOpen = false;
            exitRequested = false;
            playerController?.UnlockMovement();

            // Reset combo layer weight — single source of truth for cleanup
            var animator = controller?.GetAnimator();
            if (animator != null && meleeData != null)
                animator.SetLayerWeight(meleeData.comboLayerIndex, 0f);

            // Tell EquipmentController we're done so UpdateLayerWeight can resume
            controller?.OnComboAttackEnded();

            // Disable root motion when combo ends
            if (meleeData != null && meleeData.useRootMotion)
            {
                if (animator != null)
                    animator.applyRootMotion = false;
            }
        }

        #endregion

        #region Blade Events (Animation Events on clips)

        public override void OnBladeEnable()
        {
            Debug.Log($"[MeleeWeaponHandler] OnBladeEnable called. bladeHitbox is {(bladeHitbox != null ? "not null" : "null")}");
            if (bladeHitbox != null)
            {
                bladeHitbox.StartContinuousDetection();
                bladeHitbox.OnHitDetected -= OnBladeHit;
                bladeHitbox.OnHitDetected += OnBladeHit;
            }
        }

        public override void OnBladeDisable()
        {
            Debug.Log($"[MeleeWeaponHandler] OnBladeDisable called. bladeHitbox is {(bladeHitbox != null ? "not null" : "null")}");
            if (bladeHitbox != null)
            {
                bladeHitbox.StopContinuousDetection();
                bladeHitbox.OnHitDetected -= OnBladeHit;
            }
        }

        private void OnBladeHit(RaycastHit hit)
        {
            ProcessHit(hit);
        }

        #endregion

        #region Hit Processing

        private void ProcessHit(RaycastHit hit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;

            // Calculate damage based on combo index
            float damage = meleeData?.GetDamageForCombo(currentComboIndex) ?? 10f;
            Debug.Log($"[MeleeWeaponHandler] ProcessHit: Hit {hit.collider.name} (Tag: {hit.collider.tag}) at point {hitPoint}. Calculated damage: {damage} (Combo Index: {currentComboIndex})");

            // Apply damage to IDamageable targets (players, enemies, destructibles)
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            Debug.Log($"[MeleeWeaponHandler] Target has IDamageable: {damageable != null}, IsAlive: {damageable?.IsAlive}");
            
            if (damageable != null && damageable.IsAlive)
            {
                DamageInfo damageInfo = new DamageInfo(
                    damage,
                    DamageType.Physical,
                    hitPoint,
                    controller.NetworkObjectId
                ).WithDirection((hit.point - controller.transform.position).normalized);

                if (controller.IsServer)
                {
                    Debug.Log($"[MeleeWeaponHandler] Applying damage directly to target on Server.");
                    damageable.TakeDamage(damageInfo);
                }
                else if (controller.IsClient && controller.IsOwner)
                {
                    var targetNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                    Debug.Log($"[MeleeWeaponHandler] Attacking client is Owner. targetNetObj found: {targetNetObj != null}");
                    if (targetNetObj != null)
                    {
                        Debug.Log($"[MeleeWeaponHandler] Sending ApplyMeleeDamageServerRpc for targetNetworkObjectId: {targetNetObj.NetworkObjectId}, damage: {damage}");
                        controller.ApplyMeleeDamageServerRpc(
                            targetNetObj.NetworkObjectId,
                            damage,
                            hitPoint,
                            damageInfo.hitDirection
                        );
                    }
                    else
                    {
                        // Fallback for non-networked IDamageable on client
                        Debug.Log($"[MeleeWeaponHandler] Fallback: Target IDamageable has no NetworkObject. Calling TakeDamage locally on client.");
                        damageable.TakeDamage(damageInfo);
                    }
                }
                else
                {
                    Debug.Log($"[MeleeWeaponHandler] Hit processed but controller is neither Server nor local Owner. Damage skipped.");
                }
            }
            else
            {
                Debug.Log($"[MeleeWeaponHandler] Hit ignored because target is not IDamageable or already dead.");
            }

            // Spawn hit effect locally on the client/host who performed the hit
            // in real-time with zero delay, parented to the hit collider's transform
            SpawnHitEffect(hitPoint, hitNormal, hit.collider.transform);
            ReduceDurability(1);
        }

        #endregion

        #region Effects

        private void SpawnHitEffect(Vector3 position, Vector3 normal, Transform parent = null)
        {
            GameObject effectPrefab = null;

            // Try to get a target-specific hit effect (like blood from the PlayerStatsManager)
            if (parent != null)
            {
                var targetStats = parent.GetComponentInParent<PlayerStatsManager>();
                if (targetStats != null && targetStats.BloodHitEffectPrefab != null)
                {
                    effectPrefab = targetStats.BloodHitEffectPrefab;
                }
            }

            // Fallback to the weapon's default hit effect if the target doesn't specify one
            if (effectPrefab == null)
            {
                effectPrefab = meleeWeaponComponent?.hitEffectPrefab;
            }

            if (effectPrefab != null)
            {
                var effect = Object.Instantiate(effectPrefab, position, Quaternion.LookRotation(normal), parent);
                Object.Destroy(effect, 2f);
            }

            if (meleeWeaponComponent?.hitSound != null)
            {
                AudioPool.Play(meleeWeaponComponent.hitSound, position);
            }
        }

        private void PlaySwingSound()
        {
            if (meleeWeaponComponent?.swingSound != null && equippedObject != null)
            {
                AudioPool.Play(meleeWeaponComponent.swingSound, equippedObject.transform.position);
            }
        }

        #endregion
    }
}