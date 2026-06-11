using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for InventoryItemData with professional visual styling.
    /// </summary>
    [CustomEditor(typeof(InventoryItemData))]
    public class InventoryItemDataEditor : UnityEditor.Editor
    {
        // Serialized properties
        private SerializedProperty itemIdProp;
        private SerializedProperty itemNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty itemIconProp;
        private SerializedProperty objectTypeProp;
        private SerializedProperty maxStackProp;
        private SerializedProperty dropAsStackProp;
        private SerializedProperty maxDurabilityProp;
        private SerializedProperty localPrefabProp;
        private SerializedProperty networkPrefabProp;
        private SerializedProperty handOffsetDataProp;
        private SerializedProperty toolDataProp;
        private SerializedProperty consumableDataProp;
        private SerializedProperty meleeWeaponDataProp;
        private SerializedProperty shooterWeaponDataProp;
        private SerializedProperty chargedWeaponDataProp;
        private SerializedProperty equipSettingsProp;

        // Foldout states
        private bool handOffsetFoldout = true;
        private bool toolDataFoldout = true;
        private bool consumableDataFoldout = true;
        private bool meleeDataFoldout = true;
        private bool shooterDataFoldout = true;
        private bool chargedDataFoldout = true;

        // Cached textures and styles
        private static Texture2D headerTex;
        private static Texture2D sectionBgTex;
        private static Texture2D darkBgTex;
        private static GUIStyle headerStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle iconPreviewStyle;
        private static bool stylesInitialized;

        // Colors
        private static readonly Color HeaderColorStart = new Color(0.2f, 0.4f, 0.7f);
        private static readonly Color HeaderColorEnd = new Color(0.15f, 0.3f, 0.5f);
        private static readonly Color SectionBgColor = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color DarkBgColor = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color AccentBlue = new Color(0.3f, 0.6f, 0.95f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.75f, 0.35f);
        private static readonly Color AccentOrange = new Color(0.9f, 0.6f, 0.2f);
        private static readonly Color LabelColor = new Color(0.7f, 0.7f, 0.7f);

        private void OnEnable()
        {
            itemIdProp = serializedObject.FindProperty("itemId");
            itemNameProp = serializedObject.FindProperty("itemName");
            descriptionProp = serializedObject.FindProperty("description");
            itemIconProp = serializedObject.FindProperty("itemIcon");
            objectTypeProp = serializedObject.FindProperty("objectType");
            maxStackProp = serializedObject.FindProperty("maxStack");
            dropAsStackProp = serializedObject.FindProperty("dropAsStack");
            maxDurabilityProp = serializedObject.FindProperty("maxDurability");
            localPrefabProp = serializedObject.FindProperty("localPrefab");
            networkPrefabProp = serializedObject.FindProperty("networkPrefab");
            handOffsetDataProp = serializedObject.FindProperty("handOffsetData");
            equipSettingsProp = serializedObject.FindProperty("equipSettings");
            toolDataProp = serializedObject.FindProperty("toolData");
            consumableDataProp = serializedObject.FindProperty("consumableData");
            meleeWeaponDataProp = serializedObject.FindProperty("meleeWeaponData");
            shooterWeaponDataProp = serializedObject.FindProperty("shooterWeaponData");
            chargedWeaponDataProp = serializedObject.FindProperty("chargedWeaponData");
        }

        private void InitStyles()
        {
            if (stylesInitialized && headerTex != null) return;

            // Create gradient header texture
            headerTex = new Texture2D(1, 32);
            for (int y = 0; y < 32; y++)
            {
                float t = y / 31f;
                headerTex.SetPixel(0, y, Color.Lerp(HeaderColorEnd, HeaderColorStart, t));
            }
            headerTex.Apply();

            // Section background
            sectionBgTex = MakeTex(2, 2, SectionBgColor);
            darkBgTex = MakeTex(2, 2, DarkBgColor);

            // Header style
            headerStyle = new GUIStyle()
            {
                normal = { background = headerTex, textColor = Color.white },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(0, 0, 8, 4)
            };

            // Section style
            sectionStyle = new GUIStyle()
            {
                normal = { background = sectionBgTex },
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // Icon preview style
            iconPreviewStyle = new GUIStyle()
            {
                normal = { background = darkBgTex },
                padding = new RectOffset(4, 4, 4, 4)
            };

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            ObjectType type = (ObjectType)objectTypeProp.enumValueIndex;

            // Embedded item banner
            if (AssetDatabase.IsSubAsset(target))
            {
                DrawBanner("Embedded in ItemDatabase", AccentBlue, () => ItemDatabaseWindow.Open());
            }

            // ═══════════════════════════════════════════════════════
            // ITEM OVERVIEW HEADER
            // ═══════════════════════════════════════════════════════
            DrawItemHeader(type);

            GUILayout.Space(4);

            // ═══════════════════════════════════════════════════════
            // BASIC INFO SECTION
            // ═══════════════════════════════════════════════════════
            GUILayout.Label("ITEM DETAILS", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                DrawPropertyRow("Name", itemNameProp);
                DrawPropertyRow("Description", descriptionProp);
                
                GUILayout.Space(4);
                DrawDivider();
                GUILayout.Space(4);
                
                EditorGUILayout.BeginHorizontal();
                DrawPropertyRow("Max Stack", maxStackProp, 120);
                if (type == ObjectType.Weapon || type == ObjectType.Tools)
                {
                    GUILayout.Space(20);
                    DrawPropertyRow("Durability", maxDurabilityProp, 120);
                }
                else if (type == ObjectType.Consumable || type == ObjectType.Resource)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.PropertyField(dropAsStackProp, new GUIContent("Drop As Stack", 
                        "If true, drops spawn as one pickup with count. If false, spawn individual items."));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(2);

            // ═══════════════════════════════════════════════════════
            // PREFABS SECTION
            // ═══════════════════════════════════════════════════════
            GUILayout.Label("PREFABS", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                bool showLocalPrefab = type != ObjectType.Resource;
                // For resources, show local prefab only if canEquipToHand is enabled
                if (type == ObjectType.Resource)
                {
                    var canEquipPropCheck = equipSettingsProp.FindPropertyRelative("canEquipToHand");
                    if (canEquipPropCheck != null && canEquipPropCheck.boolValue)
                        showLocalPrefab = true;
                }
                if (showLocalPrefab)
                {
                    DrawPropertyRow("Local Prefab", localPrefabProp);
                }
                DrawPropertyRow("Network Prefab", networkPrefabProp);
            }
            EditorGUILayout.EndVertical();

            // ═══════════════════════════════════════════════════════
            // HAND PLACEMENT (for equippable items)
            // ═══════════════════════════════════════════════════════
            GUILayout.Space(2);
            if (type == ObjectType.Resource)
            {
                DrawResourceEquipSection();
            }
            else
            {
                DrawEquipAndHandSection(type);
            }

            // ═══════════════════════════════════════════════════════
            // TYPE-SPECIFIC DATA
            // ═══════════════════════════════════════════════════════
            GUILayout.Space(2);
            switch (type)
            {
                case ObjectType.Weapon:
                    bool hasMelee = meleeWeaponDataProp.objectReferenceValue != null;
                    bool hasShooter = shooterWeaponDataProp.objectReferenceValue != null;
                    bool hasCharged = chargedWeaponDataProp.objectReferenceValue != null;

                    if (hasMelee || (!hasShooter && !hasCharged))
                    {
                        DrawDataFoldout("MELEE WEAPON DATA", ref meleeDataFoldout, meleeWeaponDataProp,
                            () => CreateEmbeddedData<MeleeWeaponData>(meleeWeaponDataProp),
                            () => CreateAndAssignData<MeleeWeaponData>("MeleeWeaponData", meleeWeaponDataProp),
                            new Color(0.9f, 0.35f, 0.35f),
                            () => RemoveEmbeddedData(meleeWeaponDataProp));
                    }
                    
                    if (hasShooter || (!hasMelee && !hasCharged))
                    {
                        GUILayout.Space(2);
                        DrawDataFoldout("SHOOTER WEAPON DATA", ref shooterDataFoldout, shooterWeaponDataProp,
                            () => CreateEmbeddedData<ShooterWeaponData>(shooterWeaponDataProp),
                            () => CreateAndAssignData<ShooterWeaponData>("ShooterWeaponData", shooterWeaponDataProp),
                            new Color(0.95f, 0.6f, 0.2f),
                            () => RemoveEmbeddedData(shooterWeaponDataProp));
                    }

                    if (hasCharged || (!hasMelee && !hasShooter))
                    {
                        GUILayout.Space(2);
                        DrawDataFoldout("CHARGED WEAPON DATA", ref chargedDataFoldout, chargedWeaponDataProp,
                            () => CreateEmbeddedData<ChargedWeaponData>(chargedWeaponDataProp),
                            () => CreateAndAssignData<ChargedWeaponData>("ChargedWeaponData", chargedWeaponDataProp),
                            new Color(0.4f, 0.8f, 0.5f),
                            () => RemoveEmbeddedData(chargedWeaponDataProp));
                    }
                    break;
                case ObjectType.Tools:
                    DrawDataFoldout("TOOL DATA", ref toolDataFoldout, toolDataProp,
                        () => CreateEmbeddedData<ToolData>(toolDataProp),
                        () => CreateAndAssignData<ToolData>("ToolData", toolDataProp),
                        new Color(0.4f, 0.7f, 0.9f));
                    break;
                case ObjectType.Consumable:
                    DrawDataFoldout("CONSUMABLE DATA", ref consumableDataFoldout, consumableDataProp,
                        () => CreateEmbeddedData<ConsumableData>(consumableDataProp),
                        () => CreateAndAssignData<ConsumableData>("ConsumableData", consumableDataProp),
                        AccentGreen);
                    break;
                case ObjectType.Resource:
                    // Resources show equip section above; no type-specific data needed
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawItemHeader(ObjectType type)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.BeginHorizontal();
            
            // Icon preview
            EditorGUILayout.BeginVertical(iconPreviewStyle, GUILayout.Width(64), GUILayout.Height(64));
            {
                var icon = itemIconProp.objectReferenceValue as Sprite;
                if (icon != null)
                {
                    var rect = GUILayoutUtility.GetRect(56, 56);
                    DrawSprite(rect, icon);
                }
                else
                {
                    GUILayout.Label("No Icon", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(56));
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);

            // Item info
            EditorGUILayout.BeginVertical();
            {
                // ID Badge
                EditorGUILayout.BeginHorizontal();
                DrawBadge($"ID: {itemIdProp.intValue}", AccentBlue);
                GUILayout.Space(8);
                DrawBadge(type.ToString().ToUpper(), GetTypeColor(type));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Name
                string name = itemNameProp.stringValue;
                EditorGUILayout.LabelField(string.IsNullOrEmpty(name) ? "(Unnamed Item)" : name, 
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });

                GUILayout.Space(2);

                // Icon field
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Icon:", GUILayout.Width(35));
                EditorGUILayout.PropertyField(itemIconProp, GUIContent.none, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawBanner(string text, Color color, System.Action onClick)
        {
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
            if (GUILayout.Button("Open Window", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                onClick?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = oldBg;
            GUILayout.Space(4);
        }

        private void DrawBadge(string text, Color color)
        {
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, new GUIStyle("Badge") { 
                fontSize = 10, 
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 2, 2)
            });
            GUI.backgroundColor = oldBg;
        }

        private Color GetTypeColor(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Weapon: return new Color(0.9f, 0.35f, 0.35f);
                case ObjectType.Tools: return new Color(0.4f, 0.7f, 0.9f);
                case ObjectType.Consumable: return AccentGreen;
                case ObjectType.Resource: return AccentOrange;
                default: return Color.gray;
            }
        }

        /// <summary>
        /// Draws a sprite correctly, handling sprite sheets by using texture coordinates.
        /// </summary>
        private void DrawSprite(Rect position, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.textureRect;
            
            // Calculate UV coordinates
            Rect texCoords = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            // Maintain aspect ratio
            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = position.width / position.height;
            
            Rect drawRect = position;
            if (spriteAspect > rectAspect)
            {
                // Sprite is wider - fit to width
                float newHeight = position.width / spriteAspect;
                drawRect.y += (position.height - newHeight) / 2f;
                drawRect.height = newHeight;
            }
            else
            {
                // Sprite is taller - fit to height
                float newWidth = position.height * spriteAspect;
                drawRect.x += (position.width - newWidth) / 2f;
                drawRect.width = newWidth;
            }

            GUI.DrawTextureWithTexCoords(drawRect, tex, texCoords);
        }

        private void DrawPropertyRow(string label, SerializedProperty prop, float labelWidth = 100)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, new GUIStyle(EditorStyles.label) { normal = { textColor = LabelColor } }, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(prop, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDivider()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        }

        /// <summary>
        /// Draws the redesigned Equipment section with organized sub-groups.
        /// </summary>
        private void DrawEquipAndHandSection(ObjectType type)
        {
            // Header with orange accent
            var headerRect = EditorGUILayout.GetControlRect(false, 26);
            EditorGUI.DrawRect(headerRect, new Color(AccentOrange.r * 0.3f, AccentOrange.g * 0.3f, AccentOrange.b * 0.3f));
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), AccentOrange);
            
            var foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 16, 18);
            handOffsetFoldout = EditorGUI.Foldout(foldoutRect, handOffsetFoldout, "EQUIPMENT", true, 
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 11 });

            // Status indicators
            bool hasHandOffset = handOffsetDataProp.objectReferenceValue != null;
            if (hasHandOffset)
            {
                var checkRect = new Rect(headerRect.xMax - 24, headerRect.y + 5, 16, 16);
                GUI.Label(checkRect, "✓", new GUIStyle() { fontSize = 14, normal = { textColor = AccentGreen } });
            }

            if (!handOffsetFoldout) return;

            EditorGUILayout.BeginVertical(sectionStyle);

            // ── General ──
            DrawSubSectionHeader("⚙  General");
            EditorGUI.indentLevel++;
            
            var changeIDProp = equipSettingsProp.FindPropertyRelative("changeID");
            
            EditorGUILayout.PropertyField(changeIDProp, new GUIContent("Change ID", "Drives which equip/swap animation plays (ChangeID parameter in animator)."));
            
            EditorGUI.indentLevel--;
            GUILayout.Space(6);
            DrawDivider();
            GUILayout.Space(6);

            // ── Animation Layers ──
            DrawSubSectionHeader("🎭  Animation Layers");
            EditorGUI.indentLevel++;
            
            var equipLayerProp = equipSettingsProp.FindPropertyRelative("animationLayerIndex");
            var actionLayerProp = equipSettingsProp.FindPropertyRelative("actionLayerIndex");
            var holdLayerProp = equipSettingsProp.FindPropertyRelative("holdLayerIndex");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Equip Layer", GUILayout.Width(100));
            equipLayerProp.intValue = EditorGUILayout.IntField(equipLayerProp.intValue, GUILayout.Width(50));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Action Layer", GUILayout.Width(100));
            actionLayerProp.intValue = EditorGUILayout.IntField(actionLayerProp.intValue, GUILayout.Width(50));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Hold Layer", GUILayout.Width(100));
            holdLayerProp.intValue = EditorGUILayout.IntField(holdLayerProp.intValue, GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Help text
            EditorGUILayout.LabelField("(-1 = disabled / same as equip layer)", 
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });
            
            EditorGUI.indentLevel--;
            GUILayout.Space(6);
            DrawDivider();
            GUILayout.Space(6);

            // ── Animation ──
            DrawSubSectionHeader("🎬  Animation");
            EditorGUI.indentLevel++;
            
            var overrideProp = equipSettingsProp.FindPropertyRelative("animatorOverride");
            EditorGUILayout.PropertyField(overrideProp, new GUIContent("Animator Override", "Animator override controller for this item's animations."));
            var actionIDProp = equipSettingsProp.FindPropertyRelative("actionID");
            EditorGUILayout.PropertyField(actionIDProp, new GUIContent("Action ID", "Selects which action animation to play. Items sharing the same animation use the same value. -1 = no action animation."));
            EditorGUI.indentLevel--;
            GUILayout.Space(6);
            DrawDivider();
            GUILayout.Space(6);

            // ── Hand Placement ──
            DrawSubSectionHeader("✋  Hand Placement");
            if (!hasHandOffset)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("No hand offset configured", new GUIStyle(EditorStyles.miniLabel) { 
                    normal = { textColor = LabelColor },
                    alignment = TextAnchor.MiddleCenter
                });
                GUILayout.Space(4);
                
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = AccentOrange;
                if (GUILayout.Button("+ Create Hand Offset Data", GUILayout.Height(26)))
                {
                    CreateEmbeddedData<HandOffsetData>(handOffsetDataProp);
                }
                GUI.backgroundColor = oldBg;
                EditorGUI.indentLevel--;
            }
            else
            {
                var editor = UnityEditor.Editor.CreateEditor(handOffsetDataProp.objectReferenceValue);
                editor.OnInspectorGUI();
                DestroyImmediate(editor);
            }
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the Equipment section for Resource items.
        /// Shows canEquipToHand toggle, and when enabled reveals the full equip settings.
        /// </summary>
        private void DrawResourceEquipSection()
        {
            // Header with orange accent
            var headerRect = EditorGUILayout.GetControlRect(false, 26);
            EditorGUI.DrawRect(headerRect, new Color(AccentOrange.r * 0.3f, AccentOrange.g * 0.3f, AccentOrange.b * 0.3f));
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), AccentOrange);
            
            var foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 16, 18);
            handOffsetFoldout = EditorGUI.Foldout(foldoutRect, handOffsetFoldout, "EQUIPMENT", true, 
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 11 });

            // Status indicator
            var canEquipProp = equipSettingsProp.FindPropertyRelative("canEquipToHand");
            if (canEquipProp != null && canEquipProp.boolValue)
            {
                var checkRect = new Rect(headerRect.xMax - 24, headerRect.y + 5, 16, 16);
                GUI.Label(checkRect, "✓", new GUIStyle() { fontSize = 14, normal = { textColor = AccentGreen } });
            }

            if (!handOffsetFoldout) return;

            EditorGUILayout.BeginVertical(sectionStyle);

            // ── Can Equip Toggle ──
            DrawSubSectionHeader("⚙  General");
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(canEquipProp, new GUIContent("Can Equip To Hand", "If true, this resource can be equipped to the hand like a weapon or tool."));
            
            EditorGUI.indentLevel--;

            // Only show the rest of equip settings when canEquipToHand is enabled
            if (canEquipProp != null && canEquipProp.boolValue)
            {
                GUILayout.Space(6);
                DrawDivider();
                GUILayout.Space(6);

                // ── General Settings ──
                DrawSubSectionHeader("🔧  Equip Settings");
                EditorGUI.indentLevel++;

                var changeIDProp = equipSettingsProp.FindPropertyRelative("changeID");
                EditorGUILayout.PropertyField(changeIDProp, new GUIContent("Change ID", "Drives which equip/swap animation plays (ChangeID parameter in animator)."));

                EditorGUI.indentLevel--;
                GUILayout.Space(6);
                DrawDivider();
                GUILayout.Space(6);

                // ── Animation Layers ──
                DrawSubSectionHeader("🎭  Animation Layers");
                EditorGUI.indentLevel++;
                
                var equipLayerProp = equipSettingsProp.FindPropertyRelative("animationLayerIndex");
                var actionLayerProp = equipSettingsProp.FindPropertyRelative("actionLayerIndex");
                var holdLayerProp = equipSettingsProp.FindPropertyRelative("holdLayerIndex");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Equip Layer", GUILayout.Width(100));
                equipLayerProp.intValue = EditorGUILayout.IntField(equipLayerProp.intValue, GUILayout.Width(50));
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Action Layer", GUILayout.Width(100));
                actionLayerProp.intValue = EditorGUILayout.IntField(actionLayerProp.intValue, GUILayout.Width(50));
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Hold Layer", GUILayout.Width(100));
                holdLayerProp.intValue = EditorGUILayout.IntField(holdLayerProp.intValue, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField("(-1 = disabled / same as equip layer)", 
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });
                
                EditorGUI.indentLevel--;
                GUILayout.Space(6);
                DrawDivider();
                GUILayout.Space(6);

                // ── Animation ──
                DrawSubSectionHeader("🎬  Animation");
                EditorGUI.indentLevel++;
                
                var overrideProp = equipSettingsProp.FindPropertyRelative("animatorOverride");
                EditorGUILayout.PropertyField(overrideProp, new GUIContent("Animator Override", "Animator override controller for this item's animations."));
                var actionIDProp = equipSettingsProp.FindPropertyRelative("actionID");
                EditorGUILayout.PropertyField(actionIDProp, new GUIContent("Action ID", "Selects which action animation to play. -1 = no action animation."));
                EditorGUI.indentLevel--;
                GUILayout.Space(6);
                DrawDivider();
                GUILayout.Space(6);

                // ── Hand Placement ──
                DrawSubSectionHeader("✋  Hand Placement");
                bool hasHandOffset = handOffsetDataProp.objectReferenceValue != null;
                if (!hasHandOffset)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("No hand offset configured", new GUIStyle(EditorStyles.miniLabel) { 
                        normal = { textColor = LabelColor },
                        alignment = TextAnchor.MiddleCenter
                    });
                    GUILayout.Space(4);
                    
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = AccentOrange;
                    if (GUILayout.Button("+ Create Hand Offset Data", GUILayout.Height(26)))
                    {
                        CreateEmbeddedData<HandOffsetData>(handOffsetDataProp);
                    }
                    GUI.backgroundColor = oldBg;
                    EditorGUI.indentLevel--;
                }
                else
                {
                    var editor = UnityEditor.Editor.CreateEditor(handOffsetDataProp.objectReferenceValue);
                    editor.OnInspectorGUI();
                    DestroyImmediate(editor);
                }
            }
            else
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Enable 'Can Equip To Hand' to configure equipment settings.", 
                    new GUIStyle(EditorStyles.miniLabel) { 
                        normal = { textColor = LabelColor },
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    });
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a styled mini-header for sub-sections within the equipment panel.
        /// </summary>
        private void DrawSubSectionHeader(string title)
        {
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            var labelRect = new Rect(rect.x + 6, rect.y + 1, rect.width - 12, rect.height - 2);
            GUI.Label(labelRect, title, new GUIStyle(EditorStyles.boldLabel) { 
                fontSize = 10, 
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            });
        }

        private void DrawInfoBox(string message)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField(message, new GUIStyle(EditorStyles.miniLabel) { 
                normal = { textColor = LabelColor },
                alignment = TextAnchor.MiddleCenter
            });
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a foldout with optional create button for ScriptableObject data.
        /// </summary>
        private void DrawDataFoldout(string title, ref bool foldout, SerializedProperty prop,
            System.Action createEmbedded, System.Action createExternal, Color accentColor, System.Action onRemove = null)
        {
            bool hasData = prop.objectReferenceValue != null;
            
            // Header with accent color
            var headerRect = EditorGUILayout.GetControlRect(false, 26);
            EditorGUI.DrawRect(headerRect, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f));
            
            // Left accent bar
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), accentColor);
            
            // Calculate available width for foldout
            float buttonWidth = 0;
            if (hasData)
            {
                buttonWidth += 20; // Checkmark
                if (onRemove != null) buttonWidth += 25; // Trash button
            }

            // Foldout
            var foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 16 - buttonWidth, 18);
            foldout = EditorGUI.Foldout(foldoutRect, foldout, title, true, new GUIStyle(EditorStyles.foldout) { 
                fontStyle = FontStyle.Bold,
                fontSize = 11
            });
            
            // Status indicator
            if (hasData)
            {
                // Remove button
                if (onRemove != null)
                {
                    var removeRect = new Rect(headerRect.xMax - 45, headerRect.y + 1, 22, 22);
                    if (GUI.Button(removeRect, EditorGUIUtility.IconContent("TreeEditor.Trash"), new GUIStyle(EditorStyles.iconButton)))
                    {
                        if (EditorUtility.DisplayDialog("Remove Data", "Are you sure you want to remove this data? This cannot be undone.", "Yes", "No"))
                        {
                            onRemove.Invoke();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                var checkRect = new Rect(headerRect.xMax - 20, headerRect.y + 5, 16, 16);
                GUI.Label(checkRect, "✓", new GUIStyle() { fontSize = 14, normal = { textColor = AccentGreen } });
            }

            if (!foldout) return;

            EditorGUILayout.BeginVertical(sectionStyle);
            
            if (!hasData)
            {
                EditorGUILayout.LabelField("No data configured", new GUIStyle(EditorStyles.miniLabel) { 
                    normal = { textColor = LabelColor },
                    alignment = TextAnchor.MiddleCenter
                });
                GUILayout.Space(4);
                
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = accentColor;
                if (GUILayout.Button("+ Create Data", GUILayout.Height(26)))
                {
                    createEmbedded?.Invoke();
                }
                GUI.backgroundColor = oldBg;
            }
            else
            {
                var editor = UnityEditor.Editor.CreateEditor(prop.objectReferenceValue);
                editor.OnInspectorGUI();
                DestroyImmediate(editor);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateAndAssignData<T>(string defaultName, SerializedProperty targetProperty) where T : ScriptableObject
        {
            string itemName = itemNameProp.stringValue;
            string suggestedName = !string.IsNullOrEmpty(itemName) ? $"{itemName}_{defaultName}" : defaultName;
            
            string path = EditorUtility.SaveFilePanelInProject("Save Data", suggestedName, "asset", "Choose location");
            if (!string.IsNullOrEmpty(path))
            {
                T newData = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(newData, path);
                AssetDatabase.SaveAssets();
                targetProperty.objectReferenceValue = newData;
                serializedObject.ApplyModifiedProperties();
                EditorGUIUtility.PingObject(newData);
            }
        }

        private void CreateEmbeddedData<T>(SerializedProperty targetProperty) where T : ScriptableObject
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null) return;

            string itemName = itemNameProp.stringValue;
            string dataName = !string.IsNullOrEmpty(itemName) ? $"{itemName}_{typeof(T).Name}" : typeof(T).Name;

            var newData = ScriptableObject.CreateInstance<T>();
            newData.name = dataName;
            AssetDatabase.AddObjectToAsset(newData, mainAsset);
            newData.hideFlags = HideFlags.HideInHierarchy;

            targetProperty.objectReferenceValue = newData;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
        }

        private void RemoveEmbeddedData(SerializedProperty targetProperty)
        {
            if (targetProperty.objectReferenceValue == null) return;
            
            var dataToRemove = targetProperty.objectReferenceValue;
            string assetPath = AssetDatabase.GetAssetPath(dataToRemove);
            
            // Check if it's a sub-asset
            if (AssetDatabase.IsSubAsset(dataToRemove))
            {
                AssetDatabase.RemoveObjectFromAsset(dataToRemove);
                DestroyImmediate(dataToRemove, true);
                
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (mainAsset != null)
                {
                    EditorUtility.SetDirty(mainAsset);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
            
            targetProperty.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
