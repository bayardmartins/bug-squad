using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for normal shooter weapons.
    /// Right-click to aim, left-click to fire, R to reload.
    /// Ammo is consumed from inventory resources on reload to fill the magazine.
    /// 
    /// IK is handled by ShooterIKController (activated on equip, deactivated on unequip).
    /// This handler focuses on input, state, ammo, and effects.
    /// </summary>
    public class ShooterWeaponHandler : BaseItemHandler
    {
        private ShooterWeaponData shooterData;
        private PlayerController playerController;
        private IStaminaManager staminaManager;

        // Handedness (determined from WeaponIKPreset on equip)
        private bool isLeftHanded;

        // State
        private bool isAiming;
        private bool isReloading;
        private int currentAmmo;
        private float lastFireTime;
        private float reloadTimer;

        // IK Controller reference
        private ShooterIKController ikController;

        // Camera controller reference
        // (Aim camera logic is now handled by PlayerController)

        // Weapon child references (found on prefab)
        private Transform supportHandGrip;  // "SecondaryHandGrip" child on weapon
        private Transform aimReference;     // "AimReference" child on weapon

        // Aim target provider (provides raycast aim position from camera center)
        private AimTargetProvider aimProvider;

        // Prefab-attached shooter components
        private ShooterWeapon shooterWeaponComponent;

        // Animator parameter hashes
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        #region Equip / Unequip

        protected override void OnEquipInternal()
        {
            shooterData = itemData.shooterWeaponData;
            playerController = controller.GetPlayerController();
            staminaManager = controller.GetStaminaManager();
            ikController = controller.GetShooterIKController();
            aimProvider = ikController?.GetComponent<AimTargetProvider>();
            
            // Determine handedness from WeaponIKPreset (before visual model spawns)
            isLeftHanded = false;
            if (ikController?.CharacterIKData != null)
            {
                var preset = ikController.CharacterIKData.GetPreset(itemData);
                if (preset != null)
                    isLeftHanded = !preset.isRightHandPrimary;
            }

            SetActionParameters();

            // Restore loaded ammo from inventory slot (persists across equip/unequip cycles).
            // -1 means "never loaded" (new gun) → start at 0 (empty magazine, player must reload).
            // >= 0 means previously saved ammo count.
            int savedAmmo = 0;
            if (inventorySlot >= 0)
            {
                var inventory = controller?.GetInventoryManager();
                if (inventory != null)
                {
                    var slotItem = inventory.GetItemAt(inventorySlot);
                    savedAmmo = slotItem.loadedAmmo >= 0 ? slotItem.loadedAmmo : 0;
                }
            }
            currentAmmo = savedAmmo;

            isAiming = false;
            isReloading = false;
            lastFireTime = 0f;
            reloadTimer = 0f;
        }

        protected override void OnUnequipInternal()
        {
            // Save current ammo back to inventory slot before unequipping
            SaveAmmoToSlot();

            // Reset aim state
            if (isAiming)
            {
                isAiming = false;
                controller?.SetAnimatorBool("IsAiming", false);
                playerController?.SetAiming(false);
            }
            isReloading = false;

            // Restore AimTargetProvider defaults
            aimProvider?.ResetToDefaults();

            // Deactivate IK
            ikController?.Deactivate();
            ikController = null;

            supportHandGrip = null;
            aimReference = null;
            aimProvider = null;
            shooterWeaponComponent = null;
        }

        /// <summary>
        /// Override to parent the weapon to the correct hand based on WeaponIKPreset.isRightHandPrimary.
        /// Left-hand weapons (e.g. bow) are parented to leftHandHoldPoint instead of rightHandHoldPoint.
        /// </summary>
        protected override void SpawnEquippedObject()
        {
            if (itemData.localPrefab == null || controller == null)
                return;

            // Pick the correct hand hold point based on weapon handedness
            Transform holdPoint = isLeftHanded
                ? controller.GetLeftHandHoldPoint()
                : controller.GetHoldPoint();

            equippedObject = Object.Instantiate(itemData.localPrefab, holdPoint);

            // Apply hand offsets from per-item data
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
                // Check for new ShooterWeapon component (replaced WeaponVisuals)
                shooterWeaponComponent = equippedObject.GetComponent<ShooterWeapon>();
                
                if (shooterWeaponComponent != null)
                {
                    supportHandGrip = shooterWeaponComponent.secondaryHandGrip;
                    aimReference = shooterWeaponComponent.aimReference;
                }
                else
                {
                    // Fallback to string-based search for backwards compatibility
                    supportHandGrip = equippedObject.transform.Find("SecondaryHandGrip");
                    aimReference = equippedObject.transform.Find("AimReference");
                }

                // Push per-weapon aim settings to AimTargetProvider
                if (aimProvider != null && shooterWeaponComponent != null)
                {
                    aimProvider.ApplyWeaponSettings(
                        shooterWeaponComponent.aimLayer,
                        shooterWeaponComponent.minAimRange,
                        shooterWeaponComponent.maxAimRange,
                        shooterWeaponComponent.defaultAimDistance
                    );
                }

                // Activate IK with correct handedness from preset
                if (ikController != null && shooterData != null)
                {
                    ikController.Activate(
                        itemData,
                        supportHandGrip,
                        aimReference,
                        shooterData.alignArmToAim,
                        shooterData.alignHandToAim,
                        leftHanded: isLeftHanded
                    );
                }
            }
        }

        #endregion

        #region Input & Update

        public override void OnPrimaryAction(bool pressed)
        {
            if (isReloading) return;
            if (!pressed) return;
            if (!isAiming) return;
            TryFire();
        }

        public override void OnSecondaryAction(bool pressed)
        {
            if (isReloading) return;

            isAiming = pressed;
            controller?.SetAnimatorBool("IsAiming", isAiming);

            // Notify IK controller of aim state change
            ikController?.SetAiming(isAiming);

            // Enable strafe rotation (character faces camera direction) and transitions to aim view
            playerController?.SetAiming(isAiming);

            // Sync aim state to other players
            if (controller != null && controller.IsOwner)
                controller.SyncSecondaryActionRpc(pressed);
        }

        public override void OnUpdate()
        {
            // Handle reload timer
            if (isReloading)
            {
                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0f)
                {
                    // Reload timer fallback (normally animation event handles this)
                    FinishReload();
                }
            }

            // Manual reload with R key
            if (!isReloading && Input.GetKeyDown(KeyCode.R))
            {
                TryReload();
            }
        }

        #endregion

        #region Fire Logic

        private void TryFire()
        {
            if (shooterData == null) return;
            if (!isAiming) return;

            // Check fire rate
            if (Time.time < lastFireTime + shooterData.FireInterval)
                return;

            // Check ammo
            if (currentAmmo <= 0)
            {
                PlaySound(shooterWeaponComponent?.emptySound);
                // Auto-reload if out of ammo
                TryReload();
                return;
            }

            Fire();
        }

        private void Fire()
        {
            lastFireTime = Time.time;
            currentAmmo--;

            // Trigger fire animation (suppress layer control to keep IK/Hold layer active)
            TriggerAction(force: false, suppressLayerControl: true);

            // Spawn muzzle flash
            SpawnMuzzleFlash();

            // Play fire sound
            PlaySound(shooterWeaponComponent?.fireSound);

            // Reduce durability
            ReduceDurability(1);

            // Sync to other players
            if (controller != null && controller.IsOwner)
                controller.SyncPrimaryActionRpc(true);

            // Raycast/projectile hit detection
            SpawnProjectile(shooterData.baseDamage);
        }

        #endregion



        #region Reload Logic

        private void TryReload()
        {
            if (isReloading) return;
            if (shooterData == null) return;
            if (currentAmmo >= shooterData.magazineSize) return;

            // Check if player has ammo in inventory
            var inventory = controller?.GetInventoryManager();
            if (inventory == null) return;

            int availableAmmo = inventory.GetTotalCount(shooterData.ammoItemId);
            if (availableAmmo <= 0) return;

            StartReload();
        }

        private void StartReload()
        {
            isReloading = true;
            reloadTimer = shooterData?.reloadTime ?? 2f;

            // Cancel aim during reload
            if (isAiming)
            {
                isAiming = false;
                controller?.SetAnimatorBool("IsAiming", false);
                ikController?.SetAiming(false);
                playerController?.SetAiming(false);
            }

            // Notify IK controller
            ikController?.SetReloading(true);

            // Trigger reload animation
            var animator = controller?.GetAnimator();
            animator?.SetTrigger(ReloadHash);

            // Play reload sound
            PlaySound(shooterWeaponComponent?.reloadSound);
        }

        /// <summary>
        /// Called by animation event (OnReloadComplete) when reload animation finishes.
        /// Transfers ammo from inventory to magazine.
        /// </summary>
        public override void OnReloadComplete()
        {
            FinishReload();
        }

        private void FinishReload()
        {
            if (!isReloading) return;
            isReloading = false;
            reloadTimer = 0f;

            // Notify IK controller
            ikController?.SetReloading(false);

            if (shooterData == null) return;

            var inventory = controller?.GetInventoryManager();
            if (inventory == null) return;

            // Calculate how many rounds to load
            int needed = shooterData.magazineSize - currentAmmo;
            int available = inventory.GetTotalCount(shooterData.ammoItemId);
            int toLoad = Mathf.Min(needed, available);

            if (toLoad > 0)
            {
                // Find and consume ammo from inventory
                inventory.RemoveItemByIDRpc(shooterData.ammoItemId, toLoad);
                currentAmmo += toLoad;
            }
        }

        #endregion

        #region Properties

        /// <summary>Whether the weapon is currently in aim state.</summary>
        public bool IsAiming => isAiming;

        /// <summary>Whether the weapon is currently reloading.</summary>
        public bool IsReloading => isReloading;

        /// <summary>Current ammo in magazine.</summary>
        public int CurrentAmmo => currentAmmo;

        /// <summary>Maximum magazine size.</summary>
        public int MagazineSize => shooterData?.magazineSize ?? 0;

        #endregion

        #region Ammo Persistence

        /// <summary>
        /// Saves the current magazine ammo count back to the inventory slot.
        /// Called on unequip and weapon switch so ammo persists.
        /// </summary>
        public void SaveAmmoToSlot()
        {
            if (controller == null || inventorySlot < 0) return;

            var inventory = controller.GetInventoryManager();
            if (inventory == null) return;

            // Update the slot's loadedAmmo via server RPC
            inventory.SetLoadedAmmoRpc(inventorySlot, currentAmmo);
        }

        #endregion

        #region Effects & Shooting

        private void SpawnMuzzleFlash()
        {
            if (shooterWeaponComponent == null) return;

            if (shooterWeaponComponent.muzzleFlash != null)
            {
                shooterWeaponComponent.muzzleFlash.Play(true);
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
        /// Spawns a projectile from the muzzle point toward the aim target.
        /// Uses AimTargetProvider for the target position (camera-center raycast).
        /// The projectile is spawned on the server via EquipmentController RPC.
        /// </summary>
        private void SpawnProjectile(float damage)
        {
            if (controller == null) return;
            
            // Need the new component to determine what and where to shoot
            if (shooterWeaponComponent == null || shooterWeaponComponent.projectilePrefab == null) return;

            // Get spawn position from prefab component
            Transform spawnPoint = shooterWeaponComponent.GetShootPoint();

            // Get aim target position from AimTargetProvider
            Vector3 targetPos;
            if (aimProvider != null)
            {
                targetPos = aimProvider.AimPosition;
            }
            else
            {
                // Fallback: shoot forward from muzzle
                targetPos = spawnPoint.position + spawnPoint.forward * (shooterWeaponComponent.maxRange > 0f ? shooterWeaponComponent.maxRange : 100f);
            }

            // Calculate direction from muzzle to aim target
            Vector3 direction = (targetPos - spawnPoint.position).normalized;

            // Default tier to 1 unless there's a specific tier system for shooter weapons
            int tier = 1; 

            // Fire via server RPC
            controller.FireProjectileRpc(
                controller.NetworkObject.NetworkObjectId,
                spawnPoint.position,
                direction,
                shooterWeaponComponent.projectileSpeed,
                damage,
                (int)DamageType.Piercing,
                tier
            );
        }

        #endregion
    }
}