using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Matching rule - how to align when snapping.
    /// </summary>
    public enum MatchingRule
    {
        /// <summary>
        /// Snap point forwards face OPPOSITE each other (face-to-face alignment).
        /// Used for edge-to-edge connections like floor tiles.
        /// </summary>
        Face2Face,
        
        /// <summary>
        /// OUR snap point's forward OPPOSES the TARGET BuildPiece's forward.
        /// Our snap point faces opposite to where the target object is facing.
        /// </summary>
        Face2Object,
        
        /// <summary>
        /// OUR BuildPiece's forward OPPOSES the TARGET snap point's forward.
        /// Our whole piece faces opposite to where the target snap point is pointing.
        /// </summary>
        Object2Face
    }

    /// <summary>
    /// Where the rotation pivot is when a piece is snapped.
    /// </summary>
    public enum RotationPivotMode
    {
        /// <summary>Rotate around the snap connection point. Piece orbits around the edge/corner.</summary>
        SnapPoint,
        /// <summary>Rotate around the piece's center point (or custom pivot Transform). Piece spins in place.</summary>
        CenterPoint
    }

    /// <summary>
    /// Configuration for what this snap point can connect to.
    /// </summary>
    [System.Serializable]
    public class SnapCompatibility
    {
        [Tooltip("Target snap type this can connect to")]
        public SnapType targetType;
        
        [Tooltip("How to align when connecting to this type")]
        public MatchingRule matchingRule = MatchingRule.Face2Face;

        [Tooltip("Where to rotate around when snapped. SnapPoint = rotate around connection. CenterPoint = rotate around piece center.")]
        public RotationPivotMode rotationPivotMode = RotationPivotMode.SnapPoint;

        [Tooltip("Rotation step in degrees per key press. 90 = quarter turn, 180 = flip.")]
        public float rotationStep = 90f;

        [Tooltip("Stability decay factor when connecting through this snap. 1.0 = no decay (perfect support), 0.9 = 10% decay, 0.7 = 30% decay. Lower values = weaker connection.")]
        public float stabilityDecayFactor = 0.9f;
    }

    public class SnapPoint : MonoBehaviour
    {
        [Header("Snap Point Identity")]
        public SnapType snapType;
        
        [Header("Compatibility")]
        [Tooltip("When going to Snap this as Ghost Object, which types this will Snap With and how")]
        public SnapCompatibility[] canSnapWith;
        
        public SnapType SnapType => snapType;
        public Vector3 Forward => transform.forward;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.05f);
            
            // Forward (Yellow) -> Direction the snap faces
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.3f);
            
            // Up (Blue) -> Orientation reference (crucial for rotation)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.2f);
        }

        /// <summary>
        /// Check if this snap point can connect to a target snap type.
        /// </summary>
        public bool CanSnapTo(SnapType targetType)
        {
            if (canSnapWith == null) return false;
            
            foreach (var compat in canSnapWith)
            {
                if (compat.targetType == targetType)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get the compatibility configuration for a target snap type.
        /// Returns null if not compatible.
        /// </summary>
        public SnapCompatibility GetCompatibility(SnapType targetType)
        {
            if (canSnapWith == null) return null;
            
            foreach (var compat in canSnapWith)
            {
                if (compat.targetType == targetType)
                    return compat;
            }
            return null;
        }

        public void RegisterSnapPoint()
        {
            SnapPointRegistry.Instance.Register(this);
        }

        private void OnDestroy()
        {
            if (SnapPointRegistry.Instance != null)
                SnapPointRegistry.Instance.Unregister(this);
        }
    }

    public enum SnapType
    {
        FloorEdge,
        WallTop,
        WallSide,
        WallBottom,
        StairBottom,
        StairTop,
        RoofTop,
        RoofBottom,
        RoofEdge,
        Furniture,
        Surface // For placing objects on floors/tables loosely
    }
}
