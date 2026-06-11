using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Categories for build pieces.
    /// </summary>
    public enum BuildCategory
    {
        Floor,
        Wall,
        Roof,
        Stair,
        Decoration,
        Furniture
    }

    /// <summary>
    /// Data for a single build piece. Stored as a sub-asset in BuildDatabase.
    /// Can only be created through the Build Database Window.
    /// </summary>
    public class BuildPieceEntry : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this build piece. Auto-assigned by BuildDatabase.")]
        public int pieceId;
        
        [Tooltip("Display name for this build piece.")]
        public string pieceName;
        
        [TextArea(2, 4)]
        [Tooltip("Description shown in build UI.")]
        public string description;
        
        [Tooltip("Icon shown in build menu.")]
        public Sprite icon;

        [Header("Category")]
        [Tooltip("Category this piece belongs to (affects filtering in build menu).")]
        public BuildCategory category;

        [Header("Prefabs")]
        [Tooltip("Preview prefab shown while placing (transparent ghost).")]
        public GameObject ghostPrefab;
        
        [Tooltip("Final prefab spawned when building is confirmed.")]
        public GameObject buildPrefab;

        [Header("Costs")]
        [Tooltip("Resources required to build this piece.")]
        public ResourceCost[] costs;

        [Header("Requirements")]
        [Tooltip("Minimum player level required to build this piece.")]
        [Min(1)]
        public int requiredLevel = 1;

        [Header("Structural")]
        [Tooltip("If true, this piece acts as a foundation (stability = 100%) when placed on ground. Typically used for foundation blocks and pillars.")]
        public bool isFoundation = false;
    }

    /// <summary>
    /// Resource cost for building.
    /// </summary>
    [System.Serializable]
    public class ResourceCost
    {
        public InventoryItemData resource;
        public int amount;
    }
}
