using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Standalone HUD element that displays info about the currently equipped item:
    ///   • Item icon
    ///   • Item count in the selected slot (e.g. "x1", "x30")
    ///   • Current ammo in magazine (for shooter/bow weapons)
    ///   • Total ammo reserve in inventory (with ammo icon)
    ///
    /// The entire holder hides when no item is in hand.
    /// The ammo section only appears for weapons that consume ammo.
    ///
    /// Setup (Unity hierarchy):
    ///   EquipmentInfoUI (this script + CanvasGroup)
    ///     └─ ItemHolder (GameObject)
    ///         ├─ ItemIcon (Image)
    ///         ├─ ItemCountText (TMP_Text)           — "x30"
    ///         └─ AmmoHolder (GameObject)
    ///             ├─ CurrentAmmoText (TMP_Text)      — "12"  (magazine)
    ///             ├─ AmmoSeparator (TMP_Text)         — "/"  (optional divider)
    ///             ├─ AmmoIcon (Image)
    ///             └─ TotalAmmoText (TMP_Text)         — "120" (inventory reserve)
    /// </summary>
    public class EquipmentInfoUI : MonoBehaviour
    {
        public static EquipmentInfoUI Instance { get; private set; }

        [Header("Root Holder — hidden when nothing equipped")]
        [SerializeField] private GameObject itemHolder;

        [Header("Item Info")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemCountText;

        [Header("Ammo Section — hidden when item has no ammo")]
        [SerializeField] private GameObject ammoHolder;
        [SerializeField] private TMP_Text currentAmmoText;
        [SerializeField] private TMP_Text totalAmmoText;
        [SerializeField] private Image ammoIcon;

        [Header("Animation")]
        [Tooltip("Time in seconds to fade in/out (0 = instant)")]
        [SerializeField] private float fadeDuration = 0.2f;

        // Cached references (resolved once per equip cycle)
        private EquipmentController equipmentController;
        private IInventoryManager inventoryManager;
        private QuickInventoryUI quickInventoryUI;

        // Fade
        private CanvasGroup canvasGroup;
        private float currentAlpha;
        private float targetAlpha;

        // Cached state to avoid per-frame alloc / lookup
        private int lastItemId = -1;
        private bool hasAmmo;
        private int ammoItemId = -1;
        private InventoryItemData cachedAmmoItemData;

        // Throttle dependency resolution to avoid per-frame FindObjectsByType
        private float nextResolveTime;
        private const float RESOLVE_INTERVAL = 0.5f;

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            currentAlpha = 0f;
            targetAlpha = 0f;
            canvasGroup.alpha = 0f;

            // Start hidden
            if (itemHolder != null) itemHolder.SetActive(false);
            if (ammoHolder != null) ammoHolder.SetActive(false);
        }

        private void Update()
        {
            ResolveDependencies();
            UpdateEquipmentInfo();
            UpdateFade();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Dependency Resolution

        /// <summary>
        /// Lazily finds the local player's EquipmentController & InventoryManager.
        /// These live on the player prefab which spawns at runtime.
        /// </summary>
        private void ResolveDependencies()
        {
            if (quickInventoryUI == null)
                quickInventoryUI = QuickInventoryUI.Instance;

            if (equipmentController == null && Time.time >= nextResolveTime)
            {
                nextResolveTime = Time.time + RESOLVE_INTERVAL;

                // Find the local player's EquipmentController
                var controllers = FindObjectsByType<EquipmentController>(FindObjectsSortMode.None);
                foreach (var ec in controllers)
                {
                    if (ec.IsOwner)
                    {
                        equipmentController = ec;
                        inventoryManager = ec.GetInventoryManager();
                        break;
                    }
                }
            }
        }

        #endregion

        #region Core Update

        private void UpdateEquipmentInfo()
        {
            if (equipmentController == null || inventoryManager == null)
            {
                SetVisible(false);
                return;
            }

            // Get current equipped item data
            InventoryItemData itemData = equipmentController.GetCurrentItemData();

            // Nothing equipped → hide
            if (itemData == null)
            {
                SetVisible(false);
                lastItemId = -1;
                return;
            }

            // Get the selected slot's inventory item (for count)
            int slotIndex = equipmentController.CurrentSlotIndex;
            InventoryManager.InventoryItem slotItem = (slotIndex >= 0)
                ? inventoryManager.GetItemAt(slotIndex)
                : InventoryManager.InventoryItem.Empty;

            if (slotItem.IsEmpty)
            {
                SetVisible(false);
                lastItemId = -1;
                return;
            }

            // Item changed → re-cache ammo info
            if (itemData.itemId != lastItemId)
            {
                lastItemId = itemData.itemId;
                CacheAmmoInfo(itemData);
            }

            // Show holder
            SetVisible(true);

            // --- Item Icon ---
            if (itemIcon != null)
            {
                itemIcon.sprite = itemData.itemIcon;
                itemIcon.color = (itemData.itemIcon != null) ? Color.white : Color.clear;
            }

            // --- Item Count  (e.g. "x30") ---
            if (itemCountText != null)
            {
                itemCountText.text = $"x{slotItem.count}";
            }

            // --- Ammo Section ---
            UpdateAmmoSection(itemData);
        }

        /// <summary>
        /// Caches whether this item uses ammo, and what ammo item ID it needs.
        /// Called once when the equipped item changes.
        /// </summary>
        private void CacheAmmoInfo(InventoryItemData itemData)
        {
            hasAmmo = false;
            ammoItemId = -1;
            cachedAmmoItemData = null;

            if (itemData == null) return;

            // Shooter weapons (rifles, pistols)
            if (itemData.shooterWeaponData != null && itemData.shooterWeaponData.ammoItemId >= 0)
            {
                hasAmmo = true;
                ammoItemId = itemData.shooterWeaponData.ammoItemId;
            }
            // Charged weapons (bows, crossbows)
            else if (itemData.chargedWeaponData != null && itemData.chargedWeaponData.ammoItemId >= 0)
            {
                hasAmmo = true;
                ammoItemId = itemData.chargedWeaponData.ammoItemId;
            }

            // Cache the ammo item data for its icon
            if (hasAmmo && ammoItemId >= 0)
            {
                cachedAmmoItemData = inventoryManager.GetItemData(ammoItemId);
            }
        }

        /// <summary>
        /// Updates the ammo display section (current magazine + total reserve).
        /// </summary>
        private void UpdateAmmoSection(InventoryItemData itemData)
        {
            if (ammoHolder == null) return;

            if (!hasAmmo)
            {
                ammoHolder.SetActive(false);
                return;
            }

            ammoHolder.SetActive(true);

            // --- Current Ammo (magazine / loaded) ---
            if (currentAmmoText != null)
            {
                int currentAmmo = GetCurrentAmmoInMagazine(itemData);
                currentAmmoText.text = currentAmmo.ToString();
            }

            // --- Total Ammo Reserve (in inventory) ---
            if (totalAmmoText != null)
            {
                int totalReserve = (ammoItemId >= 0) ? inventoryManager.GetTotalCount(ammoItemId) : 0;
                totalAmmoText.text = totalReserve.ToString();
            }

            // --- Ammo Icon ---
            if (ammoIcon != null)
            {
                if (cachedAmmoItemData != null && cachedAmmoItemData.itemIcon != null)
                {
                    ammoIcon.sprite = cachedAmmoItemData.itemIcon;
                    ammoIcon.color = Color.white;
                    ammoIcon.gameObject.SetActive(true);
                }
                else
                {
                    ammoIcon.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Gets the current ammo count in the weapon's magazine/chamber.
        /// - ShooterWeaponHandler: rounds left before reload (e.g. 12/30)
        /// - BowWeaponHandler: 1 when drawing/nocked, 0 when idle
        /// </summary>
        private int GetCurrentAmmoInMagazine(InventoryItemData itemData)
        {
            if (equipmentController == null) return 0;

            // Try shooter weapon handler
            if (itemData.shooterWeaponData != null)
            {
                var shooterHandler = equipmentController.GetCurrentHandler<ShooterWeaponHandler>();
                if (shooterHandler != null)
                    return shooterHandler.CurrentAmmo;
            }

            // Try bow weapon handler
            if (itemData.chargedWeaponData != null)
            {
                var bowHandler = equipmentController.GetCurrentHandler<BowWeaponHandler>();
                if (bowHandler != null)
                {
                    // Bow: 1 if arrow is nocked or being drawn, 0 if idle
                    return (bowHandler.IsNocked || bowHandler.IsDrawing) ? 1 : 0;
                }
            }

            return 0;
        }

        #endregion

        #region Visibility & Fade

        private void SetVisible(bool visible)
        {
            targetAlpha = visible ? 1f : 0f;

            if (itemHolder != null)
            {
                // Activate immediately when showing; hide after fade-out completes
                if (visible)
                    itemHolder.SetActive(true);
            }
        }

        private void UpdateFade()
        {
            if (canvasGroup == null) return;

            if (fadeDuration <= 0f)
                currentAlpha = targetAlpha;
            else
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);

            canvasGroup.alpha = currentAlpha;
            canvasGroup.blocksRaycasts = currentAlpha > 0.01f;

            // Hide holder after fully faded out
            if (currentAlpha <= 0f && itemHolder != null)
                itemHolder.SetActive(false);
        }

        #endregion
    }
}