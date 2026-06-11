using UnityEngine;
namespace Ignitives.MultiplayerEngine
{
    // [CreateAssetMenu(fileName = "InventoryItemData", menuName = "Multiplayer Engine/Inventory/Item Data", order = 1)]
    public class InventoryItemData : ScriptableObject
    {
        [Header("Basic Info")]
        public int itemId;
        public string itemName;
        [TextArea(2, 4)]
        [Tooltip("Item description - used in crafting UI")]
        public string description;
        public Sprite itemIcon;
        public ObjectType objectType;
        
        public int maxStack;
        [Tooltip("If true, dropped stacks spawn as one pickup with count. If false, spawn individual items.")]
        public bool dropAsStack = true;
        public int maxDurability = 100;

        [Header("Prefabs")]
        public GameObject localPrefab;
        public GameObject networkPrefab;

        [Header("Hand Placement")]
        [Tooltip("Position and rotation offsets for equipping in hand")]
        public HandOffsetData handOffsetData;

        [Header("Equipment Settings")]
        [Tooltip("Data-driven settings for equip behavior (animation, layer, visibility).")]
        public EquipSettings equipSettings;

        [Header("Tool Data")]
        [Tooltip("Data for tools - only used if objectType is Tools")]
        public ToolData toolData;

        [Header("Consumable Data")]
        [Tooltip("Data for consumables - only used if objectType is Consumable")]
        public ConsumableData consumableData;

        [Header("Melee Weapon Data")]
        [Tooltip("Data for melee weapons - only used if objectType is Weapon")]
        public MeleeWeaponData meleeWeaponData;

        [Header("Shooter Weapon Data")]
        [Tooltip("Data for shooter weapons - only used if objectType is Weapon")]
        public ShooterWeaponData shooterWeaponData;

        [Header("Charged Weapon Data")]
        [Tooltip("Data for charged weapons (bows, crossbows, etc.) - only used if objectType is Weapon")]
        public ChargedWeaponData chargedWeaponData;

        // Check if this item has durability
        public bool HasDurability => maxDurability > 0 && (objectType == ObjectType.Weapon || objectType == ObjectType.Tools);

        // Get durability color based on percentage
        public Color GetDurabilityColor(float durabilityPercentage)
        {
            if (durabilityPercentage > 0.6f)
                return Color.green;
            else if (durabilityPercentage > 0.3f)
                return Color.yellow;
            else
                return Color.red;
        }
    }
}
