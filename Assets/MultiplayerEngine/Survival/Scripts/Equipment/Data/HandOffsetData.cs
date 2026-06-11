using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// ScriptableObject storing hand position and rotation offsets for equipped items.
    /// Create one per unique weapon/tool grip style.
    /// </summary>
    // [CreateAssetMenu] removed as this is created via ItemDatabaseWindow
    // [CreateAssetMenu(fileName = "HandOffsetData", menuName = "Multiplayer Engine/Equipment/Hand Offset Data", order = 10)]
    public class HandOffsetData : ScriptableObject
    {
        [Tooltip("Local position offset from the hold point")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Euler rotation offset (degrees)")]
        public Vector3 rotationOffset = Vector3.zero;

        /// <summary>
        /// Apply the stored offsets to a transform.
        /// </summary>
        public void ApplyTo(Transform target)
        {
            if (target == null) return;
            target.localPosition = positionOffset;
            target.localRotation = Quaternion.Euler(rotationOffset);
        }

        /// <summary>
        /// Copy current transform values into this offset data.
        /// Used by the editor tool to save adjustments.
        /// </summary>
        public void CopyFrom(Transform source)
        {
            if (source == null) return;
            positionOffset = source.localPosition;
            rotationOffset = source.localRotation.eulerAngles;
        }
    }
}
