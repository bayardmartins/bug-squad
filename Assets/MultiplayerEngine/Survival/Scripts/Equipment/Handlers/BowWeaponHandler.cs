using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for bow weapons (and other charged projectile weapons).
    /// Secondary action (aim): grab arrow → nock → Aiming (waiting to draw).
    /// Primary action (shoot) while aiming: Drawing (charge) → release to fire.
    /// Primary action from Idle: auto-aim → grab → nock → Drawing → release to fire → idle.
    /// 
    /// State flow:
    ///   Idle → (aim OR shoot pressed) → GrabbingArrow
    ///   GrabbingArrow → (OnGrabArrow event) → spawn local arrow in draw hand
    ///   GrabbingArrow → (OnNockArrow event) → Aiming (if aim held) or Drawing (if shoot held)
    ///   GrabbingArrow → (shoot released before nock) → cancel, hide arrow, Idle
    ///   Aiming → (shoot pressed) → Drawing
    ///   Aiming → (aim released) → cancel, Idle
    ///   Drawing → (shoot released) → Fire projectile
    ///     → if aim held: auto-grab next arrow (GrabbingArrow)
    ///     → if no aim: disable aim, Idle
    ///   
    /// IK is handled by ShooterIKController (same as shooter weapons).
    /// String draw is achieved by procedurally moving the BowWeapon.stringNockPoint,
    /// which the support hand IK naturally follows.
    /// </summary>
    public class BowWeaponHandler : BaseItemHandler
    {
        private ChargedWeaponData chargedData;
        private PlayerController playerController;

        // Handedness (from WeaponIKPreset)
        private bool isLeftHanded;

        // State machine
        private enum BowState { Idle, GrabbingArrow, Aiming, Drawing, Fired }
        private BowState state;

        // Charge
        private float drawPercent;

        // Aim
        private bool isAiming;

        // Input tracking
        private bool isShootHeld;      // Whether shoot (primary) is currently held
        private bool isAimExplicit;    // Whether aim was explicitly activated via secondary action

        // IK controller
        private ShooterIKController ikController;
        private AimTargetProvider aimProvider;

        // Prefab component
        private BowWeapon bowWeaponComponent;

        // Local arrow visual (not networked)
        private GameObject localArrow;

        // Weapon child references
        private Transform supportHandGrip;
        private Transform aimReference;

        // IK speed caching
        private float originalSupportHandSpeed = 10f;

        // Animator parameter hashes
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
        private static readonly int ChargeLevelHash = Animator.StringToHash("ChargeLevel");

        #region Equip / Unequip

        protected override void OnEquipInternal()
        {
            chargedData = itemData.chargedWeaponData;
            playerController = controller.GetPlayerController();
            ikController = controller.GetShooterIKController();
            aimProvider = ikController?.GetComponent<AimTargetProvider>();

            // Determine handedness from WeaponIKPreset
            isLeftHanded = false;
            if (ikController?.CharacterIKData != null)
            {
                var preset = ikController.CharacterIKData.GetPreset(itemData);
                if (preset != null)
                    isLeftHanded = !preset.isRightHandPrimary;
            }

            SetActionParameters();

            state = BowState.Idle;
            drawPercent = 0f;
            isAiming = false;
            isShootHeld = false;
            isAimExplicit = false;

            // In Idle, the drawing hand should not be attached to the string
            ikController?.SetSupportHandDisabled(true);
        }

        protected override void OnUnequipInternal()
        {
            // Cancel any active state
            CancelDraw();

            // Reset aim and input tracking
            EnableAimMode(false);
            isShootHeld = false;
            isAimExplicit = false;

            // Restore AimTargetProvider defaults
            aimProvider?.ResetToDefaults();

            // Deactivate IK
            if (ikController != null)
            {
                ikController.SetSupportHandDisabled(false);
                ikController.supportHandSpeed = originalSupportHandSpeed; // Restore speed
                ikController.Deactivate();
            }
            ikController = null;

            supportHandGrip = null;
            aimReference = null;
            aimProvider = null;
            bowWeaponComponent = null;
        }

        /// <summary>
        /// Parent bow to correct hand based on WeaponIKPreset handedness.
        /// </summary>
        protected override void SpawnEquippedObject()
        {
            if (itemData.localPrefab == null || controller == null)
                return;

            Transform holdPoint = isLeftHanded
                ? controller.GetLeftHandHoldPoint()
                : controller.GetHoldPoint();

            equippedObject = Object.Instantiate(itemData.localPrefab, holdPoint);

            HandOffsetData activeHandOffset = itemData.handOffsetData;
            if (activeHandOffset != null)
            {
                activeHandOffset.ApplyTo(equippedObject.transform);
            }
            else
            {
                equippedObject.transform.localPosition = Vector3.zero;
                equippedObject.transform.localRotation = Quaternion.identity;
            }

            equippedObject.SetActive(true);
        }

        protected override void OnVisualModelSpawned()
        {
            base.OnVisualModelSpawned();

            if (equippedObject != null)
            {
                bowWeaponComponent = equippedObject.GetComponent<BowWeapon>();

                if (bowWeaponComponent != null)
                {
                    // The support hand (drawing hand) must grip the string nock point, not the bow body
                    supportHandGrip = bowWeaponComponent.secondaryHandGrip != null 
                        ? bowWeaponComponent.secondaryHandGrip
                        : bowWeaponComponent.stringNockPoint;
                    aimReference = bowWeaponComponent.aimReference;
                }
                else
                {
                    // Fallback
                    supportHandGrip = equippedObject.transform.Find("SecondaryHandGrip");
                    aimReference = equippedObject.transform.Find("AimReference");
                }

                // Push aim settings
                if (aimProvider != null && bowWeaponComponent != null)
                {
                    aimProvider.ApplyWeaponSettings(
                        bowWeaponComponent.aimLayer,
                        bowWeaponComponent.minAimRange,
                        bowWeaponComponent.maxAimRange,
                        bowWeaponComponent.defaultAimDistance
                    );
                }

                // Activate IK
                if (ikController != null && chargedData != null)
                {
                    // Cache original speed and slow it down for smooth 0.5s hand transition to string
                    originalSupportHandSpeed = ikController.supportHandSpeed;
                    ikController.supportHandSpeed = 6f; // ~0.5s lerp

                    ikController.Activate(
                        itemData,
                        supportHandGrip,
                        aimReference,
                        chargedData.alignArmToAim,
                        chargedData.alignHandToAim,
                        leftHanded: isLeftHanded
                    );
                }
            }
        }

        #endregion

        #region Input & Update

        public override void OnPrimaryAction(bool pressed)
        {
            if (pressed)
            {
                isShootHeld = true;

                if (state == BowState.Idle)
                {
                    // Shoot without explicit aim → auto-aim and grab arrow
                    if (!isAimExplicit)
                        EnableAimMode(true);
                    TryGrabArrow();
                }
                else if (state == BowState.Aiming)
                {
                    // Already aiming with arrow nocked → start drawing
                    StartDrawing();
                }
            }
            else
            {
                isShootHeld = false;

                if (state == BowState.Drawing)
                {
                    Fire();
                }
                else if (state == BowState.GrabbingArrow)
                {
                    // Released shoot before charge started → cancel
                    CancelDraw();
                    if (!isAimExplicit)
                        EnableAimMode(false);
                }
            }

            // Sync to remote clients
            if (controller != null && controller.IsOwner)
                controller?.SyncPrimaryActionRpc(pressed);
        }

        public override void OnSecondaryAction(bool pressed)
        {
            if (pressed)
            {
                isAimExplicit = true;
                EnableAimMode(true);

                if (state == BowState.Idle)
                {
                    TryGrabArrow();
                }
            }
            else
            {
                isAimExplicit = false;

                if (state == BowState.Aiming)
                {
                    // Release aim while aiming (not drawing) → go idle
                    CancelDraw();
                    EnableAimMode(false);
                }
                else if (state == BowState.GrabbingArrow)
                {
                    // Release aim while grabbing arrow (and shoot is not held) → cancel immediately
                    if (!isShootHeld)
                    {
                        CancelDraw();
                        EnableAimMode(false);
                    }
                }
                else if (state == BowState.Idle)
                {
                    EnableAimMode(false);
                }
                // If Drawing, don't cancel — let shoot handle it
            }

            if (controller != null && controller.IsOwner)
                controller?.SyncSecondaryActionRpc(pressed);
        }

        public override void OnUpdate()
        {
            if (state == BowState.Drawing && chargedData != null)
            {
                float chargeTime = chargedData.chargeTime > 0f ? chargedData.chargeTime : 1f;
                drawPercent = Mathf.Clamp01(drawPercent + Time.deltaTime / chargeTime);

                // Move string back via BowWeapon component
                bowWeaponComponent?.SetStringDraw(drawPercent, chargedData.drawDistance);

                // Update animator
                controller?.GetAnimator()?.SetFloat(ChargeLevelHash, drawPercent);
            }
        }

        #endregion

        #region Draw / Fire Logic

        private void TryGrabArrow()
        {
            if (chargedData == null) return;
            if (state != BowState.Idle) return;

            // Check ammo
            if (chargedData.ammoItemId >= 0)
            {
                var inventory = controller?.GetInventoryManager();
                if (inventory != null)
                {
                    int available = inventory.GetTotalCount(chargedData.ammoItemId);
                    if (available <= 0)
                    {
                        PlaySound(bowWeaponComponent?.emptySound);
                        return;
                    }
                }
            }

            state = BowState.GrabbingArrow;
            drawPercent = 0f;

            // Disable support hand IK so animation can play reach-back
            ikController?.SetSupportHandDisabled(true);

            // Trigger the grab/draw animation
            var animator = controller?.GetAnimator();
            animator?.SetBool(IsChargingHash, true);
            animator?.SetFloat(ChargeLevelHash, 0f);
        }

        /// <summary>
        /// Begins the drawing/charging phase from the Aiming state.
        /// Called when shoot is pressed while an arrow is nocked.
        /// </summary>
        private void StartDrawing()
        {
            if (state != BowState.Aiming) return;

            state = BowState.Drawing;
            drawPercent = 0f;

            // Play draw sound
            PlaySound(bowWeaponComponent?.drawSound);
        }

        /// <summary>
        /// Called by animation event when the draw hand reaches the quiver.
        /// Spawns the local (visual-only) arrow.
        /// </summary>
        public override void OnGrabArrow()
        {
            if (state != BowState.GrabbingArrow) return;

            // Spawn local arrow
            if (bowWeaponComponent?.localArrowPrefab != null)
            {
                // Ensure no old arrow is stuck in hand before spawning a new one
                if (localArrow != null) DestroyLocalArrow();

                // Spawn the arrow in the drawing hand (not the bow hand)
                Transform drawingHandPoint = isLeftHanded ? controller.GetHoldPoint() : controller.GetLeftHandHoldPoint();

                localArrow = Object.Instantiate(bowWeaponComponent.localArrowPrefab, drawingHandPoint);
                localArrow.transform.localPosition = bowWeaponComponent.arrowHandPositionOffset;
                localArrow.transform.localEulerAngles = bowWeaponComponent.arrowHandRotationOffset;
            }
        }

        /// <summary>
        /// Called by animation event when the arrow reaches the string nock point.
        /// Re-enables IK and transitions based on current input state:
        ///   - Shoot held → Drawing (charge starts immediately)
        ///   - Aim held (no shoot) → Aiming (waiting for shoot press)
        ///   - Neither held → cancel and return to Idle
        /// </summary>
        public override void OnNockArrow()
        {
            if (state != BowState.GrabbingArrow) return;

            // Re-enable support hand IK (hand will now track the secondaryHandGrip/stringNockPoint)
            // Note: The arrow stays parented to the drawing hand.
            ikController?.SetSupportHandDisabled(false);

            // Decide next state based on current input
            if (isShootHeld)
            {
                // Shoot is held → go directly to Drawing (charge starts)
                state = BowState.Drawing;
                drawPercent = 0f;
                PlaySound(bowWeaponComponent?.drawSound);
            }
            else if (isAimExplicit)
            {
                // Aim is held but shoot isn't → wait in Aiming state (arrow nocked, ready)
                state = BowState.Aiming;
            }
            else
            {
                // Neither shoot nor aim held → cancel, return to idle
                CancelDraw();
                EnableAimMode(false);
            }
        }

        private void Fire()
        {
            if (chargedData == null) return;

            float damage = chargedData.GetDamage(drawPercent);
            float speed = chargedData.GetProjectileSpeed(drawPercent);

            // Consume ammo
            if (chargedData.ammoItemId >= 0)
            {
                var inventory = controller?.GetInventoryManager();
                inventory?.RemoveItemByIDRpc(chargedData.ammoItemId, 1);
            }

            // Destroy local arrow
            DestroyLocalArrow();

            // Reset string
            bowWeaponComponent?.ResetString();

            // Reset charge state
            drawPercent = 0f;

            var animator = controller?.GetAnimator();
            animator?.SetFloat(ChargeLevelHash, 0f);

            // Trigger fire/release animation (suppress layer control to keep IK/Hold layer active)
            TriggerAction(force: true, suppressLayerControl: true);

            // Play release sound
            PlaySound(bowWeaponComponent?.releaseSound);

            // Reduce durability
            ReduceDurability(1);

            // Fire projectile via server RPC
            SpawnProjectile(damage, speed);

            // Sync to remote clients
            if (controller != null && controller.IsOwner)
                controller?.SyncPrimaryActionRpc(true);

            // Post-fire: return to idle, then auto-grab if still aiming
            state = BowState.Idle;

            // Disable support hand IK so the grab animation can play the reach-back
            ikController?.SetSupportHandDisabled(true);

            if (isAimExplicit)
            {
                // Still aiming → auto-grab next arrow for continuous shooting
                // IsCharging is kept true so the grab/nock animation replays seamlessly
                TryGrabArrow();
            }
            else
            {
                // No explicit aim → fully return to idle
                animator?.SetBool(IsChargingHash, false);
                EnableAimMode(false);
            }
        }

        private void CancelDraw()
        {
            if (state == BowState.Idle) return;

            // Destroy local arrow
            DestroyLocalArrow();

            // Reset string
            bowWeaponComponent?.ResetString();

            // Reset charge state
            state = BowState.Idle;
            drawPercent = 0f;

            var animator = controller?.GetAnimator();
            animator?.SetBool(IsChargingHash, false);
            animator?.SetFloat(ChargeLevelHash, 0f);

            // Immediately crossfade back to Idle state to cancel any active grab/draw/Reload animation
            int animLayer = itemData?.equipSettings?.animationLayerIndex ?? 1;
            controller?.PlayAnimationCrossFade("Bow.Idle", 0.15f, animLayer);

            // Disable support hand IK in Idle
            ikController?.SetSupportHandDisabled(true);
        }

        #endregion

        #region Properties

        public bool IsAiming => isAiming;
        public bool IsAimExplicit => isAimExplicit;
        public bool IsDrawing => state == BowState.Drawing;
        public bool IsNocked => state == BowState.Aiming;
        public float DrawPercent => drawPercent;

        #endregion

        #region Helpers

        /// <summary>
        /// Enables or disables aim mode visuals (animator, IK, player controller).
        /// </summary>
        private void EnableAimMode(bool enable)
        {
            isAiming = enable;
            controller?.SetAnimatorBool("IsAiming", enable);
            ikController?.SetAiming(enable);
            playerController?.SetAiming(enable);
        }

        private void DestroyLocalArrow()
        {
            if (localArrow != null)
            {
                Object.Destroy(localArrow);
                localArrow = null;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && equippedObject != null)
            {
                AudioPool.Play(clip, equippedObject.transform.position);
            }
        }

        /// <summary>
        /// Spawns a projectile from the aim reference toward the aim target.
        /// Uses AimTargetProvider for the target position (camera-center raycast).
        /// The projectile is spawned on the server via EquipmentController RPC.
        /// </summary>
        private void SpawnProjectile(float damage, float speed)
        {
            if (controller == null) return;
            if (bowWeaponComponent == null || bowWeaponComponent.projectilePrefab == null) return;

            Transform spawnPoint = bowWeaponComponent.GetShootPoint();

            // Get aim target position
            Vector3 targetPos;
            if (aimProvider != null)
            {
                targetPos = aimProvider.AimPosition;
            }
            else
            {
                targetPos = spawnPoint.position + spawnPoint.forward * 100f;
            }

            Vector3 direction = (targetPos - spawnPoint.position).normalized;
            int tier = 1;

            controller.FireProjectileRpc(
                controller.NetworkObject.NetworkObjectId,
                spawnPoint.position,
                direction,
                speed,
                damage,
                (int)DamageType.Piercing,
                tier
            );
        }

        #endregion
    }
}