using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom two-bone IK solver that operates in LateUpdate by directly
    /// manipulating bone transforms after the Animator has output its pose.
    /// 
    /// Architecture:
    ///   - Takes a bone chain: root (upper arm) → middle (lower arm/elbow) → end (hand)
    ///   - Creates hidden reference transforms to snapshot the animation pose each frame
    ///   - Offset transforms are children of the refs — their local offsets define the IK target
    ///   - AnimationToIK() snapshots the current anim frame, then solves the bone chain
    ///     so the end bone reaches the offset position
    /// 
    /// Usage: Created by ShooterIKController, called each LateUpdate frame.
    /// </summary>
    [System.Serializable]
    public class TwoBoneIKSolver
    {
        public Transform rootTransform;  // Character root (for parenting refs)
        public Transform rootBone;       // UpperArm
        public Transform middleBone;     // LowerArm (elbow)
        public Transform endBone;        // Hand

        [Header("Reference Transforms (auto-created, hidden)")]
        public Transform endBoneRef;       // Snapshot of hand pos/rot from animation
        public Transform middleBoneRef;    // Snapshot of elbow pos/rot from animation
        public Transform endBoneOffset;    // Child of endBoneRef — stores hand IK offset
        public Transform middleBoneOffset; // Child of middleBoneRef — stores hint offset

        private string endTag;
        private string middleTag;
        private float _weight;
        private Vector3? hintPosition;

        /// <summary>
        /// Auto-creates the bone chain from an Animator + IK goal.
        /// </summary>
        public TwoBoneIKSolver(Animator animator, AvatarIKGoal ikGoal)
        {
            if (animator == null) return;
            rootTransform = animator.transform;

            if (!animator.isHuman) return;

            switch (ikGoal)
            {
                case AvatarIKGoal.LeftHand:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    middleBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    endBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    endTag = "LeftHand";
                    middleTag = "LeftHint";
                    break;
                case AvatarIKGoal.RightHand:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    middleBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    endBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    endTag = "RightHand";
                    middleTag = "RightHint";
                    break;
            }

            CreateReferenceTransforms();
        }

        /// <summary>
        /// Whether all required bones and reference transforms exist.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return rootBone && middleBone && endBone &&
                       endBoneRef && middleBoneRef &&
                       endBoneOffset && middleBoneOffset;
            }
        }

        /// <summary>Current IK weight (0 = animation only, 1 = full IK).</summary>
        public float IKWeight => _weight;

        /// <summary>Set IK weight (0-1).</summary>
        public void SetIKWeight(float weight)
        {
            _weight = weight;
        }

        /// <summary>
        /// Creates hidden reference + offset GameObjects.
        /// Refs are parented to character root and snapshot the animation pose.
        /// Offsets are children of refs — their local transform is the IK offset.
        /// </summary>
        private void CreateReferenceTransforms()
        {
            if (!rootTransform || !rootBone || !middleBone || !endBone) return;

            if (!endBoneRef)
            {
                endBoneRef = new GameObject(endTag + "Ref").transform;
                endBoneRef.hideFlags = HideFlags.HideInHierarchy;
                endBoneRef.SetParent(rootTransform);
            }
            if (!middleBoneRef)
            {
                middleBoneRef = new GameObject(middleTag + "Ref").transform;
                middleBoneRef.hideFlags = HideFlags.HideInHierarchy;
                middleBoneRef.SetParent(rootTransform);
            }

            if (!endBoneOffset)
            {
                endBoneOffset = new GameObject(endTag + "Offset").transform;
                endBoneOffset.SetParent(endBoneRef);
                endBoneOffset.localPosition = Vector3.zero;
                endBoneOffset.localEulerAngles = Vector3.zero;
            }
            if (!middleBoneOffset)
            {
                middleBoneOffset = new GameObject(middleTag + "Offset").transform;
                middleBoneOffset.SetParent(middleBoneRef);
                middleBoneOffset.localPosition = Vector3.zero;
                middleBoneOffset.localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>
        /// Snapshots the current animation pose into the reference transforms.
        /// Call this before applying any IK offsets.
        /// </summary>
        public void UpdateIK()
        {
            if (endBoneRef)
            {
                endBoneRef.position = endBone.position;
                endBoneRef.rotation = endBone.rotation;
            }
            if (middleBoneRef)
            {
                middleBoneRef.position = middleBone.position;
                middleBoneRef.rotation = middleBone.rotation;
            }
        }

        /// <summary>
        /// Snapshots animation → applies offsets → solves IK.
        /// This is the main method called each LateUpdate frame.
        /// The offset transforms have their local position/rotation set by
        /// ShooterIKController from the WeaponIKPreset data.
        /// </summary>
        public void AnimationToIK()
        {
            if (!IsValid)
            {
                CreateReferenceTransforms();
                return;
            }

            UpdateIK();
            SetIKHintPosition(middleBoneOffset.position);
            SetIKPosition(endBoneOffset.position);
            SetIKRotation(endBoneOffset.rotation);
        }

        /// <summary>
        /// Solves the two-bone chain so that endBone reaches ikPosition.
        /// Uses law of cosines to compute the elbow (middleBone) position,
        /// then rotates rootBone and middleBone to align the chain.
        /// </summary>
        public void SetIKPosition(Vector3 ikPosition)
        {
            if (_weight <= 0f) return;

            // Determine elbow bend direction from hint or auto-calculate
            Vector3 middleBoneDirection;
            if (hintPosition.HasValue)
            {
                middleBoneDirection = hintPosition.Value - rootBone.position;
            }
            else
            {
                // Auto-calculate: perpendicular to the root→end axis, biased by current elbow position
                middleBoneDirection = Vector3.Cross(
                    endBone.position - rootBone.position,
                    Vector3.Cross(endBone.position - rootBone.position, endBone.position - middleBone.position)
                );
            }

            // Bone lengths
            float upperArmLength = (middleBone.position - rootBone.position).magnitude;
            float forearmLength = (endBone.position - middleBone.position).magnitude;

            // Calculate elbow position using law of cosines
            Vector3 elbowPos = CalculateElbowPosition(
                rootBone.position, ikPosition,
                upperArmLength, forearmLength,
                middleBoneDirection
            );

            // Rotate upper arm to point at computed elbow position
            Quaternion upperArmRot = Quaternion.FromToRotation(
                middleBone.position - rootBone.position,
                elbowPos - rootBone.position
            ) * rootBone.rotation;

            if (!IsNaNQuaternion(upperArmRot))
            {
                rootBone.rotation = Quaternion.Slerp(rootBone.rotation, upperArmRot, _weight);

                // Rotate forearm to point at IK target
                Quaternion forearmRot = Quaternion.FromToRotation(
                    endBone.position - middleBone.position,
                    ikPosition - elbowPos
                ) * middleBone.rotation;

                middleBone.rotation = Quaternion.Slerp(middleBone.rotation, forearmRot, _weight);
            }

            hintPosition = null;
        }

        /// <summary>
        /// Sets the rotation of the end bone (hand).
        /// </summary>
        public void SetIKRotation(Quaternion rotation)
        {
            if (!rootBone || !middleBone || !endBone || _weight <= 0f) return;
            endBone.rotation = Quaternion.Slerp(endBone.rotation, rotation, _weight);
        }

        /// <summary>
        /// Sets the hint position for the middle bone (elbow).
        /// Must be called BEFORE SetIKPosition.
        /// </summary>
        public void SetIKHintPosition(Vector3 position)
        {
            hintPosition = position;
        }

        /// <summary>
        /// Computes the elbow position using the law of cosines.
        /// Clamps the distance to prevent impossible configurations.
        /// </summary>
        private Vector3 CalculateElbowPosition(
            Vector3 rootPos, Vector3 endPos,
            float upperArmLen, float forearmLen,
            Vector3 bendDirection)
        {
            Vector3 rootToEnd = endPos - rootPos;
            float rootToEndMag = rootToEnd.magnitude;

            // Clamp to max reach (prevent locked elbow)
            float maxReach = (upperArmLen + forearmLen) * 0.999f;
            if (rootToEndMag > maxReach)
            {
                endPos = rootPos + rootToEnd.normalized * maxReach;
                rootToEnd = endPos - rootPos;
                rootToEndMag = maxReach;
            }

            // Clamp to min reach (prevent collapsed arm)
            float minReach = Mathf.Max(0.05f, Mathf.Abs(upperArmLen - forearmLen) * 1.001f);
            if (rootToEndMag < minReach)
            {
                endPos = rootPos + rootToEnd.normalized * minReach;
                rootToEnd = endPos - rootPos;
                rootToEndMag = minReach;
            }

            // Law of cosines: find distance along root→end axis to elbow projection
            float cosAngle = (rootToEndMag * rootToEndMag + upperArmLen * upperArmLen - forearmLen * forearmLen)
                             * 0.5f / rootToEndMag;

            // Perpendicular distance from axis to elbow
            float sinAngle = Mathf.Sqrt(Mathf.Max(0f, upperArmLen * upperArmLen - cosAngle * cosAngle));

            // Cross product to get the perpendicular direction
            Vector3 perpendicular = Vector3.Cross(rootToEnd, Vector3.Cross(bendDirection, rootToEnd));
            Vector3 perpDirection = perpendicular.normalized;
            if (perpendicular.sqrMagnitude < 1e-6f)
            {
                // Fallback to a perpendicular direction using a safe reference axis
                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(rootToEnd.normalized, up)) > 0.99f)
                    up = Vector3.right;
                perpDirection = Vector3.Cross(rootToEnd, up).normalized;
            }

            return rootPos + (cosAngle * rootToEnd.normalized) + (sinAngle * perpDirection);
        }

        /// <summary>
        /// Checks if a quaternion contains NaN values.
        /// </summary>
        private static bool IsNaNQuaternion(Quaternion q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        /// <summary>
        /// Destroys the runtime reference GameObjects.
        /// Call when the solver is no longer needed.
        /// </summary>
        public void Dispose()
        {
            if (endBoneRef) Object.Destroy(endBoneRef.gameObject);
            if (middleBoneRef) Object.Destroy(middleBoneRef.gameObject);
            endBoneRef = null;
            middleBoneRef = null;
            endBoneOffset = null;
            middleBoneOffset = null;
        }
    }
}