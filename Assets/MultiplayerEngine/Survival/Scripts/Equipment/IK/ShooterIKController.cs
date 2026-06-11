using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Runtime IK controller for shooter weapons.
    /// Runs in LateUpdate after the Animator, using custom TwoBoneIKSolver
    /// to directly manipulate bone transforms.
    /// 
    /// Pipeline (each LateUpdate):
    ///   1. Apply spine/head rotation offsets (dynamic pitch + per-preset static offsets)
    ///   2. Apply IK offsets to weapon hand and solve the bone chain
    ///   3. Align weapon barrel to aim target (world-space hand correction)
    ///   4. Solve support-hand IK (secondary hand targets weapon grip)
    /// 
    /// Must be placed on the same GameObject as the Animator.
    /// Activated by the weapon handlers on equip, deactivated on unequip.
    /// </summary>
    public class ShooterIKController : MonoBehaviour
    {
        [Header("Character IK Data")]
        [Tooltip("Assign the character's IK data containing weapon presets")]
        public CharacterIKData characterIKData;

        [Header("Parent Bone")]
        [Tooltip("Which bone IK offsets are relative to")]
        [SerializeField] private IKParentBone parentBone = IKParentBone.Spine;

        [Header("Aim Alignment")]
        [Tooltip("Speed of upper arm alignment to aim target")]
        public float smoothArmAlignWeight = 8f;
        [Tooltip("Speed of arm IK rotation smoothing")]
        public float smoothArmIKRotation = 20f;
        [Tooltip("Maximum angle from forward for aim alignment")]
        public float maxAimAngle = 60f;
        [Tooltip("Speed of support hand IK weight transition")]
        public float supportHandSpeed = 10f;

        [Header("Spine Vertical Aim")]
        [Tooltip("How much the spine follows camera pitch when aiming (0=none, 1=full)")]
        [Range(0f, 1f)]
        public float spineVerticalWeight = 0.6f;
        [Tooltip("Smoothing speed for spine pitch changes")]
        public float spineSmoothing = 8f;

        [Header("IK Weight")]
        [Tooltip("Speed of IK weight fade in/out")]
        public float fadeSpeed = 6f;

        [Tooltip("Duration (seconds) for IK to blend in after equipping a weapon")]
        public float equipBlendDuration = 0.5f;

        [Tooltip("Duration (seconds) for IK to blend out when starting a weapon swap")]
        public float unequipBlendDuration = 0.2f;

        [Header("Debug")]
        public bool drawGizmos = false;

        // Runtime state
        private bool isActive;
        private float masterWeight;
        private float aimBlend;
        private float supportIKWeight;
        private float equipBlendTimer;    // Counts up from 0 → equipBlendDuration after equip
        private bool isUnequipBlending;   // True while IK is fading out for swap
        private float unequipBlendTimer;  // Counts up from 0 → unequipBlendDuration during swap-out
        private float armAlignmentWeight;



        // Smoothed spine pitch for dynamic up/down aiming
        private float smoothedSpinePitch;

        // Custom IK solvers
        private TwoBoneIKSolver leftIK;
        private TwoBoneIKSolver rightIK;

        /// <summary>Exposed for Editor scripts to render handles</summary>
        public TwoBoneIKSolver LeftIK => leftIK;
        /// <summary>Exposed for Editor scripts to render handles</summary>
        public TwoBoneIKSolver RightIK => rightIK;

        // Cached references
        private Animator animator;
        private Transform parentBoneTransform;
        private AimTargetProvider aimTarget;

        // Active weapon state (set by handlers)
        private WeaponIKPreset currentPreset;
        private bool isAiming;
        private bool isReloading;
        private bool isEquipping;
        private bool isSupportHandDisabled;  // Used by BowWeaponHandler during arrow grab
        private bool isLeftWeapon;      // If true, left hand is weapon hand
        private Transform supportHandTarget;  // Weapon grip for support hand
        private Transform aimReference;       // Weapon aim reference point
        private bool alignArmToAim = true;
        private bool alignHandToAim = true;

        // Cached arm transforms
        private Transform rightUpperArm;
        private Transform leftUpperArm;
        private Transform rightHand;
        private Transform leftHand;

        // Cached spine/head transforms
        private Transform spineBone;
        private Transform chestBone;
        private Transform upperChestBone;
        private Transform headBone;

        // Reference to PlayerController for camera pitch
        private PlayerController playerController;

        #region Unity Lifecycle

        private void Awake()
        {
            animator = GetComponent<Animator>();
            aimTarget = GetComponent<AimTargetProvider>();
            playerController = GetComponentInParent<PlayerController>();
            CacheBoneTransforms();
        }

        private void LateUpdate()
        {
            // Fade master weight
            float targetWeight = isActive ? 1f : 0f;

            // Unequip blend-out: fast fade to 0 when swap starts
            if (isUnequipBlending)
            {
                unequipBlendTimer += Time.deltaTime;
                float blendSpeed = unequipBlendDuration > 0f ? 1f / unequipBlendDuration : fadeSpeed;
                masterWeight = Mathf.MoveTowards(masterWeight, 0f, blendSpeed * Time.deltaTime);
                if (masterWeight < 0.001f)
                {
                    isUnequipBlending = false;
                    return;
                }
            }
            // Equip blend-in: gradual fade from 0 after equipping
            else if (isActive && equipBlendTimer < equipBlendDuration)
            {
                equipBlendTimer += Time.deltaTime;
                float blendSpeed = equipBlendDuration > 0f ? 1f / equipBlendDuration : fadeSpeed;
                masterWeight = Mathf.MoveTowards(masterWeight, targetWeight, blendSpeed * Time.deltaTime);
            }
            else
            {
                masterWeight = Mathf.MoveTowards(masterWeight, targetWeight, fadeSpeed * Time.deltaTime);
            }

            if (masterWeight < 0.001f) return;
            if (currentPreset == null) return;
            if (isReloading || isEquipping)
            {
                // During reload/equip, fade IK out smoothly
                masterWeight = Mathf.MoveTowards(masterWeight, 0f, fadeSpeed * 2f * Time.deltaTime);
                if (masterWeight < 0.001f) return;
            }

            // Update aim blend (smooth transition between idle and aim poses)
            float aimTarget = isAiming ? 1f : 0f;
            aimBlend = Mathf.MoveTowards(aimBlend, aimTarget, 8f * Time.deltaTime);

            // Get the active IK adjust based on aim state
            IKAdjust adjust = currentPreset.GetAdjust(isAiming);

            // Step 0: Apply spine/head rotation offsets (before arm IK so arms follow torso)
            ApplySpineHeadRotation(adjust);

            // Step 1: Apply IK offsets to weapon hand and solve
            UpdateWeaponHandIK(adjust);

            // Step 2: Align weapon barrel to aim target
            if (alignArmToAim || alignHandToAim)
                AlignWeaponToAim();

            // Step 3: Apply support hand IK (grip weapon)
            UpdateSupportHandIK(adjust);
        }

        #endregion

        #region Public API — Activate/Deactivate

        /// <summary>
        /// Activates IK for a weapon. Called by weapon handlers on equip.
        /// </summary>
        /// <param name="itemData">InventoryItemData of the equipped weapon</param>
        /// <param name="gripTarget">Transform on weapon prefab for support hand (child named "SecondaryHandGrip")</param>
        /// <param name="aimRef">Transform on weapon prefab for aim reference (child named "AimReference")</param>
        /// <param name="armAlign">Whether to procedurally align upper arm to aim target</param>
        /// <param name="handAlign">Whether to procedurally align hand to aim target</param>
        /// <param name="leftHanded">Whether the weapon is held in left hand (swaps primary/secondary)</param>
        public void Activate(InventoryItemData itemData, Transform gripTarget, Transform aimRef,
                             bool armAlign = true, bool handAlign = true, bool leftHanded = false)
        {
            if (characterIKData != null)
                currentPreset = characterIKData.GetPreset(itemData);

            supportHandTarget = gripTarget;
            aimReference = aimRef;
            alignArmToAim = armAlign;
            alignHandToAim = handAlign;
            isLeftWeapon = leftHanded;
            isActive = true;
            isAiming = editorForceAim;
            isReloading = false;
            isEquipping = false;

            // Start IK at zero weight, blend in over equipBlendDuration
            masterWeight = 0f;
            equipBlendTimer = 0f;

            CacheParentBone();
            EnsureSolvers();
        }

        /// <summary>
        /// Deactivates IK. Called by weapon handlers on unequip.
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            isUnequipBlending = false;
            currentPreset = null;
            supportHandTarget = null;
            aimReference = null;
            // masterWeight will fade to 0 naturally
        }

        /// <summary>
        /// Begins a smooth IK blend-out over unequipBlendDuration.
        /// Called at the START of a swap animation so IK fades before the hand reaches the holster.
        /// </summary>
        public void BeginUnequipBlend()
        {
            if (!isActive || masterWeight < 0.001f) return;
            isUnequipBlending = true;
            unequipBlendTimer = 0f;
        }

        /// <summary>Set aim state (called by handlers). Ignored when EditorForceAim is active.</summary>
        public void SetAiming(bool aiming)
        {
            if (editorForceAim) return; // Editor override takes priority
            isAiming = aiming;
        }

        /// <summary>Set reload state (called by handlers).</summary>
        public void SetReloading(bool reloading) => isReloading = reloading;

        /// <summary>Set equipping state (called by handlers).</summary>
        public void SetEquipping(bool equipping) => isEquipping = equipping;

        /// <summary>
        /// Temporarily disable support hand IK (e.g. during bow arrow grab).
        /// When EditorForceAim is active the support hand must stay enabled for handle
        /// editing, so we stash the request for later restoration instead.
        /// </summary>
        public void SetSupportHandDisabled(bool disabled)
        {
            if (editorForceAim)
            {
                // Editor is overriding — save for when aim editing ends
                editorSavedSupportDisabled = disabled;
            }
            else
            {
                isSupportHandDisabled = disabled;
            }
        }

        /// <summary>Whether IK is currently active.</summary>
        public bool IsActive => isActive;

        /// <summary>Current aim blend (0=idle, 1=aim).</summary>
        public float AimBlend => aimBlend;

        /// <summary>Current master weight.</summary>
        public float MasterWeight => masterWeight;

        /// <summary>Gets the active preset.</summary>
        public WeaponIKPreset CurrentPreset => currentPreset;

        /// <summary>Gets the character IK data.</summary>
        public CharacterIKData CharacterIKData => characterIKData;

        /// <summary>Gets the parent bone transform for offset calculations.</summary>
        public Transform ParentBoneTransform => parentBoneTransform;

        /// <summary>Gets the support hand target (weapon grip).</summary>
        public Transform SupportHandTarget => supportHandTarget;

        /// <summary>Gets the current aim state.</summary>
        public bool IsAimingState => isAiming;

        /// <summary>Current support hand IK weight (for editor diagnostics).</summary>
        public float SupportIKWeight => supportIKWeight * masterWeight;

        /// <summary>Last computed support IK target position (for editor gizmos).</summary>
        public Vector3 DebugSupportTarget { get; private set; }

        /// <summary>
        /// Debug flag: when true, forces aim mode on (used by editor tools).
        /// Also drives the Animator's IsAiming parameter so the aim animation plays,
        /// giving the gun/body the correct aim pose for IK offset editing.
        /// When activated, temporarily overrides isSupportHandDisabled so the secondary
        /// hand IK engages (critical for bow weapons where the handler disables it in idle).
        /// </summary>
        public bool EditorForceAim
        {
            get => editorForceAim;
            set
            {
                if (editorForceAim == value) return;

                editorForceAim = value;
                isAiming = value;

                if (value)
                {
                    // Save the current support-hand-disabled state so we can restore it later.
                    // Bow weapons disable the support hand in idle (the draw hand isn't on the string),
                    // but when the editor forces aim mode we need the support hand IK to engage
                    // so that handle edits have visible feedback and the hand moves to the grip point.
                    editorSavedSupportDisabled = isSupportHandDisabled;
                    isSupportHandDisabled = false;
                }
                else
                {
                    // Restore the handler's original state
                    isSupportHandDisabled = editorSavedSupportDisabled;
                }

                // Drive the Animator's IsAiming parameter so the aim animation actually plays.
                // Without this, the gun stays in idle pose and only the IK offsets change.
                if (animator != null)
                    animator.SetBool("IsAiming", value);
            }
        }
        private bool editorForceAim;
        private bool editorSavedSupportDisabled;

        #endregion

        #region IK Pipeline

        /// <summary>
        /// Step 0: Applies spine and head rotation offsets from the IKAdjust.
        /// These are additive rotations applied BEFORE arm IK so the arms follow the torso.
        /// spineOffset.x = pitch (up/down), spineOffset.y = yaw (left/right), spineOffset.z = roll
        /// headOffset.x = pitch, headOffset.y = yaw, headOffset.z = roll
        /// </summary>
        private void ApplySpineHeadRotation(IKAdjust adjust)
        {
            if (adjust == null) return;

            // --- Step 1: Static spine/head offsets from IKAdjust (per-weapon preset) ---
            // Applied FIRST so the spine settles into its designer-set rest pose.
            // These are bone-local corrections (yaw, pitch, roll) that position the
            // upper body correctly for each weapon's hold position.
            if (spineBone != null && adjust.spineOffset != Vector3.zero)
            {
                Quaternion spineRot = Quaternion.Euler(adjust.spineOffset.x, adjust.spineOffset.y, adjust.spineOffset.z);
                spineBone.localRotation *= Quaternion.Slerp(Quaternion.identity, spineRot, masterWeight);
            }

            if (headBone != null && adjust.headOffset != Vector3.zero)
            {
                Quaternion headRot = Quaternion.Euler(adjust.headOffset.x, adjust.headOffset.y, adjust.headOffset.z);
                headBone.localRotation *= Quaternion.Slerp(Quaternion.identity, headRot, masterWeight);
            }

            // --- Step 2: Dynamic spine pitch from camera look angle ---
            // When aiming, bend the spine up/down based on where the camera is looking.
            // Distributed across Spine, Chest, and UpperChest for natural bending.
            float targetPitch = 0f;
            if (isAiming && playerController != null)
            {
                // CameraPitch is negative when looking up, positive when looking down.
                // We want spine to pitch in the same direction.
                targetPitch = playerController.CameraPitch * spineVerticalWeight;
            }
            smoothedSpinePitch = Mathf.Lerp(smoothedSpinePitch, targetPitch, spineSmoothing * Time.deltaTime);

            // Distribute dynamic pitch across spine bones (roughly 40% / 35% / 25%).
            //
            // Two critical details make this work correctly with static offsets:
            //
            // 1. AXIS: We rotate around the character's RIGHT axis (world space) converted
            //    to each bone's parent local space. This ensures pitch is always relative to
            //    the character's forward, not the bone's own (potentially yawed) frame.
            //
            // 2. PRE-MULTIPLICATION: We use  dynRot * localRotation  instead of
            //    localRotation *= dynRot. Post-multiply applies in the bone's OWN rotated
            //    frame (skewed by static yaw/roll offsets). Pre-multiply applies in the
            //    PARENT's local space which is equivalent to world space, so the pitch axis
            //    stays aligned to the character's right regardless of any static offsets.
            float pitchWeight = masterWeight;
            if (Mathf.Abs(smoothedSpinePitch) > 0.01f)
            {
                Vector3 characterRight = transform.right;

                if (spineBone != null)
                {
                    Vector3 localAxis = spineBone.parent != null
                        ? spineBone.parent.InverseTransformDirection(characterRight).normalized
                        : characterRight;
                    Quaternion dynRot = Quaternion.AngleAxis(smoothedSpinePitch * 0.4f, localAxis);
                    Quaternion weighted = Quaternion.Slerp(Quaternion.identity, dynRot, pitchWeight);
                    spineBone.localRotation = weighted * spineBone.localRotation;
                }
                if (chestBone != null)
                {
                    Vector3 localAxis = chestBone.parent != null
                        ? chestBone.parent.InverseTransformDirection(characterRight).normalized
                        : characterRight;
                    Quaternion dynRot = Quaternion.AngleAxis(smoothedSpinePitch * 0.35f, localAxis);
                    Quaternion weighted = Quaternion.Slerp(Quaternion.identity, dynRot, pitchWeight);
                    chestBone.localRotation = weighted * chestBone.localRotation;
                }
                if (upperChestBone != null)
                {
                    Vector3 localAxis = upperChestBone.parent != null
                        ? upperChestBone.parent.InverseTransformDirection(characterRight).normalized
                        : characterRight;
                    Quaternion dynRot = Quaternion.AngleAxis(smoothedSpinePitch * 0.25f, localAxis);
                    Quaternion weighted = Quaternion.Slerp(Quaternion.identity, dynRot, pitchWeight);
                    upperChestBone.localRotation = weighted * upperChestBone.localRotation;
                }
            }
        }

        /// <summary>
        /// Step 1: Applies IK offsets to the weapon (primary) hand solver and solves.
        /// Reads offsets from IKAdjust, applies them to the solver's offset transforms,
        /// then runs AnimationToIK() to solve the bone chain.
        /// </summary>
        private void UpdateWeaponHandIK(IKAdjust adjust)
        {
            TwoBoneIKSolver weaponSolver = isLeftWeapon ? leftIK : rightIK;
            if (weaponSolver == null || !weaponSolver.IsValid) return;

            weaponSolver.SetIKWeight(masterWeight);

            if (adjust != null)
            {
                // Apply weapon hand offset
                ApplyOffset(adjust.primaryHandOffset, weaponSolver.endBoneOffset);
                ApplyOffset(adjust.primaryHintOffset, weaponSolver.middleBoneOffset);
            }

            // Solve: snapshot animation → apply offsets → solve bone chain
            weaponSolver.AnimationToIK();
        }

        /// <summary>
        /// Step 2: Aligns the weapon barrel (aimReference.forward) to point at the aim target.
        /// 
        /// Works by computing a world-space rotation delta between where the barrel IS
        /// currently pointing and where it SHOULD point, then applying that correction
        /// directly to the hand bone's world rotation. This correctly propagates through
        /// the entire weapon hierarchy regardless of HandOffsetData, IKAdjust offsets,
        /// or spine rotation offsets.
        /// 
        /// Smoothing comes from armAlignmentWeight's transition speed (smoothArmAlignWeight)
        /// rather than accumulated quaternion smoothing, avoiding stale-correction drift.
        /// </summary>
        private void AlignWeaponToAim()
        {
            if (this.aimTarget == null) return;
            if (aimReference == null) return;

            // Smoothly blend alignment weight in/out based on aim state
            bool canAlign = isAiming && masterWeight > 0.01f;
            armAlignmentWeight = canAlign
                ? Mathf.Lerp(armAlignmentWeight, 1f, smoothArmAlignWeight * Time.deltaTime)
                : Mathf.Lerp(armAlignmentWeight, 0f, smoothArmAlignWeight * 2f * Time.deltaTime);

            if (armAlignmentWeight < 0.01f) return;

            Vector3 aimPoint = this.aimTarget.AimPosition;

            // Check aim angle limit (prevent aiming behind the character horizontally)
            Transform aimAngleRef = parentBoneTransform ?? transform;
            Vector3 aimDir = aimPoint - aimAngleRef.position;
            Vector3 horizontalAimDir = new Vector3(aimDir.x, 0f, aimDir.z);
            Vector3 horizontalForward = new Vector3(aimAngleRef.forward.x, 0f, aimAngleRef.forward.z);
            if (horizontalAimDir != Vector3.zero && horizontalForward != Vector3.zero)
            {
                float angle = Vector3.Angle(horizontalAimDir, horizontalForward);
                if (angle > maxAimAngle) return;
            }

            Transform hand = isLeftWeapon ? leftHand : rightHand;
            if (hand == null) return;

            // Current barrel direction (where the gun is actually pointing after all IK/offsets)
            Vector3 currentBarrelForward = aimReference.forward;

            // Desired direction (from barrel position to aim point)
            Vector3 desiredDirection = (aimPoint - aimReference.position).normalized;

            // World-space rotation delta to align barrel with aim target
            Quaternion correction = Quaternion.FromToRotation(currentBarrelForward, desiredDirection);

            if (IsNaNQuaternion(correction)) return;

            // Apply correction to hand bone in world space, weighted by alignment blend
            // This rotates the entire hand (and thus the weapon) so the barrel aligns with the target
            Quaternion correctedHandRot = correction * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, correctedHandRot, armAlignmentWeight);
        }

        /// <summary>
        /// Step 3: Makes the support (off) hand grip the weapon.
        /// 
        /// The secondary hand is fundamentally different from the primary:
        /// - Primary: offsets are animation-relative (fine-tune the animation pose)
        /// - Secondary: offsets are GRIP-relative (fine-tune position on the weapon grip)
        /// 
        /// Pipeline:
        ///   1. Always snapshot animation pose (keeps editor handles positioned correctly)
        ///   2. Compute IK target = grip position + per-pose offset in grip-local space
        ///   3. Apply hint offset (animation-relative, for elbow direction)
        ///   4. Solve the 2-bone chain so the support hand reaches the adjusted grip target
        /// </summary>
        private void UpdateSupportHandIK(IKAdjust adjust)
        {
            if (currentPreset == null || !currentPreset.useSecondaryHand) return;

            TwoBoneIKSolver supportSolver = isLeftWeapon ? rightIK : leftIK;
            if (supportSolver == null || !supportSolver.IsValid) return;

            // Always snapshot animation pose so editor handles stay positioned correctly
            // (even when IK weight is zero, the ref transforms must track the bones)
            supportSolver.UpdateIK();

            // Determine if support hand should be active
            bool useSupport = supportHandTarget != null && !isReloading && !isEquipping && !isSupportHandDisabled;
            float targetWeight = useSupport ? 1f : 0f;
            supportIKWeight = Mathf.Lerp(supportIKWeight, targetWeight, supportHandSpeed * Time.deltaTime);

            if (supportIKWeight < 0.01f)
            {
                supportSolver.SetIKWeight(0f);
                return;
            }

            supportSolver.SetIKWeight(supportIKWeight * masterWeight);

            if (supportHandTarget == null) return;

            // Compute IK target: grip position + per-pose offset in grip-local space
            Vector3 targetPos = supportHandTarget.position;
            Quaternion targetRot = supportHandTarget.rotation;

            if (adjust != null)
            {
                // TransformPoint applies the offset in the grip target's local coordinate space
                // This is consistent with the editor which uses reference.TransformPoint(offset.position)
                targetPos = supportHandTarget.TransformPoint(adjust.secondaryHandOffset.position);
                targetRot = supportHandTarget.rotation * Quaternion.Euler(adjust.secondaryHandOffset.eulerAngles);

                // Hint offset is animation-relative (applied to solver's offset transform)
                ApplyOffset(adjust.secondaryHintOffset, supportSolver.middleBoneOffset);
            }

            // Cache for debug visualization
            DebugSupportTarget = targetPos;

            // Solve the bone chain
            supportSolver.SetIKHintPosition(supportSolver.middleBoneOffset.position);
            supportSolver.SetIKPosition(targetPos);
            supportSolver.SetIKRotation(targetRot);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Applies an IKOffsetTransform to a solver's offset transform.
        /// </summary>
        private void ApplyOffset(IKOffsetTransform offset, Transform target)
        {
            if (offset == null || target == null) return;
            target.localPosition = offset.position;
            target.localEulerAngles = offset.eulerAngles;
        }

        /// <summary>
        /// Ensures both IK solvers are created and valid.
        /// </summary>
        private void EnsureSolvers()
        {
            if (animator == null) return;

            if (leftIK == null || !leftIK.IsValid)
                leftIK = new TwoBoneIKSolver(animator, AvatarIKGoal.LeftHand);

            if (rightIK == null || !rightIK.IsValid)
                rightIK = new TwoBoneIKSolver(animator, AvatarIKGoal.RightHand);
        }

        /// <summary>
        /// Caches the parent bone transform for offset calculations.
        /// </summary>
        private void CacheParentBone()
        {
            if (animator == null) return;
            parentBoneTransform = parentBone switch
            {
                IKParentBone.Spine => animator.GetBoneTransform(HumanBodyBones.Spine),
                IKParentBone.Chest => animator.GetBoneTransform(HumanBodyBones.Chest),
                IKParentBone.UpperChest => animator.GetBoneTransform(HumanBodyBones.UpperChest),
                _ => animator.GetBoneTransform(HumanBodyBones.Spine)
            };
        }

        /// <summary>
        /// Caches references to arm and hand bones.
        /// </summary>
        private void CacheBoneTransforms()
        {
            if (animator == null || !animator.isHuman) return;

            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private static bool IsNaNQuaternion(Quaternion q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        private void OnDestroy()
        {
            leftIK?.Dispose();
            rightIK?.Dispose();
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (!isActive) return;

            // Always draw support IK debug when active (critical for diagnosing)
            TwoBoneIKSolver supportSolver = isLeftWeapon ? rightIK : leftIK;
            if (supportSolver != null && supportSolver.IsValid && supportIKWeight > 0.01f)
            {
                // Green sphere = IK target (where we're solving TO)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(DebugSupportTarget, 0.035f);

                // Blue sphere = actual hand bone position
                Gizmos.color = new Color(0.3f, 0.5f, 1f);
                Gizmos.DrawWireSphere(supportSolver.endBone.position, 0.025f);

                // Line from hand to target
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(supportSolver.endBone.position, DebugSupportTarget);
            }

            if (!drawGizmos) return;

            // Support hand grip point
            if (supportHandTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(supportHandTarget.position, 0.03f);
                Gizmos.DrawLine(supportHandTarget.position, supportHandTarget.position + supportHandTarget.forward * 0.1f);
            }

            // Aim reference
            if (aimReference != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(aimReference.position, 0.02f);
                Gizmos.DrawRay(aimReference.position, aimReference.forward * 0.5f);
            }

            // Aim target
            if (this.aimTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(this.aimTarget.AimPosition, 0.05f);
            }
        }

        #endregion
    }
}