using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Stores position and rotation offsets for a single IK target (hand or hint).
    /// </summary>
    [System.Serializable]
    public class IKOffsetTransform
    {
        public Vector3 position;
        public Vector3 eulerAngles;
    }

    /// <summary>
    /// Per-pose IK offset data. Contains offsets for both hands (weapon + support)
    /// and their elbow hints, plus spine/head rotation offsets.
    /// One IKAdjust is stored per character state (idle, aiming).
    /// </summary>
    [System.Serializable]
    public class IKAdjust
    {
        [Header("Primary Hand (Weapon Hand)")]
        public IKOffsetTransform primaryHandOffset = new IKOffsetTransform();
        public IKOffsetTransform primaryHintOffset = new IKOffsetTransform();

        [Header("Secondary Hand (Support Hand)")]
        public IKOffsetTransform secondaryHandOffset = new IKOffsetTransform();
        public IKOffsetTransform secondaryHintOffset = new IKOffsetTransform();

        [Header("Spine / Head Rotation Offsets")]
        [Tooltip("X/Y/Z rotation offset applied to spine bone (pitch/yaw/roll)")]
        public Vector3 spineOffset;
        [Tooltip("X/Y/Z rotation offset applied to head bone (pitch/yaw/roll)")]
        public Vector3 headOffset;

        /// <summary>
        /// Creates a deep copy of this IKAdjust.
        /// </summary>
        public IKAdjust Copy()
        {
            return new IKAdjust
            {
                primaryHandOffset = new IKOffsetTransform
                {
                    position = primaryHandOffset.position,
                    eulerAngles = primaryHandOffset.eulerAngles
                },
                primaryHintOffset = new IKOffsetTransform
                {
                    position = primaryHintOffset.position,
                    eulerAngles = primaryHintOffset.eulerAngles
                },
                secondaryHandOffset = new IKOffsetTransform
                {
                    position = secondaryHandOffset.position,
                    eulerAngles = secondaryHandOffset.eulerAngles
                },
                secondaryHintOffset = new IKOffsetTransform
                {
                    position = secondaryHintOffset.position,
                    eulerAngles = secondaryHintOffset.eulerAngles
                },
                spineOffset = spineOffset,
                headOffset = headOffset
            };
        }
    }
}