using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for BuildPieceEntry with professional visual styling.
    /// Similar to InventoryItemDataEditor.
    /// </summary>
    [CustomEditor(typeof(BuildPieceEntry))]
    public class BuildPieceEntryEditor : UnityEditor.Editor
    {
        // Serialized properties
        private SerializedProperty pieceIdProp;
        private SerializedProperty pieceNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty iconProp;
        private SerializedProperty categoryProp;
        private SerializedProperty ghostPrefabProp;
        private SerializedProperty buildPrefabProp;
        private SerializedProperty costsProp;

        private SerializedProperty requiredLevelProp;

        // Foldout states
        private bool costsFoldout = true;
        private bool requirementsFoldout = true;

        // Cached textures and styles
        private static Texture2D headerTex;
        private static Texture2D sectionBgTex;
        private static Texture2D darkBgTex;
        private static GUIStyle headerStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle iconPreviewStyle;
        private static bool stylesInitialized;

        // Colors
        private static readonly Color HeaderColorStart = new Color(0.3f, 0.5f, 0.2f);
        private static readonly Color HeaderColorEnd = new Color(0.2f, 0.35f, 0.15f);
        private static readonly Color SectionBgColor = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color DarkBgColor = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.75f, 0.35f);
        private static readonly Color AccentBlue = new Color(0.3f, 0.6f, 0.95f);
        private static readonly Color AccentOrange = new Color(0.9f, 0.6f, 0.2f);
        private static readonly Color LabelColor = new Color(0.7f, 0.7f, 0.7f);

        private void OnEnable()
        {
            pieceIdProp = serializedObject.FindProperty("pieceId");
            pieceNameProp = serializedObject.FindProperty("pieceName");
            descriptionProp = serializedObject.FindProperty("description");
            iconProp = serializedObject.FindProperty("icon");
            categoryProp = serializedObject.FindProperty("category");
            ghostPrefabProp = serializedObject.FindProperty("ghostPrefab");
            buildPrefabProp = serializedObject.FindProperty("buildPrefab");
            costsProp = serializedObject.FindProperty("costs");

            requiredLevelProp = serializedObject.FindProperty("requiredLevel");
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

            BuildCategory category = (BuildCategory)categoryProp.enumValueIndex;

            // Embedded piece banner
            if (AssetDatabase.IsSubAsset(target))
            {
                DrawBanner("Embedded in BuildDatabase", AccentGreen, () => BuildDatabaseWindow.Open());
            }

            // ═══════════════════════════════════════════════════════
            // PIECE OVERVIEW HEADER
            // ═══════════════════════════════════════════════════════
            DrawPieceHeader(category);

            GUILayout.Space(4);

            // ═══════════════════════════════════════════════════════
            // BASIC INFO SECTION
            // ═══════════════════════════════════════════════════════
            GUILayout.Label("PIECE DETAILS", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                DrawPropertyRow("Name", pieceNameProp);
                DrawPropertyRow("Description", descriptionProp);
                
                GUILayout.Space(4);
                DrawDivider();
                GUILayout.Space(4);
                
                DrawPropertyRow("Category", categoryProp);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(2);

            // ═══════════════════════════════════════════════════════
            // PREFABS SECTION
            // ═══════════════════════════════════════════════════════
            GUILayout.Label("PREFABS", headerStyle);
            EditorGUILayout.BeginVertical(sectionStyle);
            {
                DrawPropertyRow("Ghost Prefab", ghostPrefabProp);
                DrawPropertyRow("Build Prefab", buildPrefabProp);
                
                // Validation warning
                if (buildPrefabProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Build Prefab is required for this piece to be placeable!", MessageType.Warning);
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(2);

            // ═══════════════════════════════════════════════════════
            // COSTS SECTION
            // ═══════════════════════════════════════════════════════
            DrawFoldoutSection("RESOURCE COSTS", ref costsFoldout, AccentOrange, () =>
            {
                EditorGUILayout.PropertyField(costsProp, GUIContent.none, true);
            });

            GUILayout.Space(2);

            // ═══════════════════════════════════════════════════════
            // REQUIREMENTS SECTION
            // ═══════════════════════════════════════════════════════
            DrawFoldoutSection("REQUIREMENTS", ref requirementsFoldout, AccentBlue, () =>
            {

                DrawPropertyRow("Required Level", requiredLevelProp, 120);
            });

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPieceHeader(BuildCategory category)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.BeginHorizontal();
            
            // Icon preview
            EditorGUILayout.BeginVertical(iconPreviewStyle, GUILayout.Width(64), GUILayout.Height(64));
            {
                var icon = iconProp.objectReferenceValue as Sprite;
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

            // Piece info
            EditorGUILayout.BeginVertical();
            {
                // ID and Category badges
                EditorGUILayout.BeginHorizontal();
                DrawBadge($"ID: {pieceIdProp.intValue}", AccentBlue);
                GUILayout.Space(8);
                DrawBadge(category.ToString().ToUpper(), GetCategoryColor(category));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Name
                string name = pieceNameProp.stringValue;
                EditorGUILayout.LabelField(string.IsNullOrEmpty(name) ? "(Unnamed Piece)" : name, 
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });

                GUILayout.Space(2);

                // Icon field
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Icon:", GUILayout.Width(35));
                EditorGUILayout.PropertyField(iconProp, GUIContent.none, GUILayout.Width(150));
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

        private Color GetCategoryColor(BuildCategory category)
        {
            return category switch
            {
                BuildCategory.Floor => new Color(0.6f, 0.4f, 0.2f),
                BuildCategory.Wall => new Color(0.5f, 0.5f, 0.6f),
                BuildCategory.Roof => new Color(0.7f, 0.3f, 0.3f),
                BuildCategory.Stair => new Color(0.4f, 0.6f, 0.5f),
                BuildCategory.Decoration => new Color(0.7f, 0.5f, 0.7f),
                BuildCategory.Furniture => new Color(0.5f, 0.6f, 0.4f),
                _ => Color.gray
            };
        }

        private void DrawSprite(Rect position, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.textureRect;
            
            Rect texCoords = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = position.width / position.height;
            
            Rect drawRect = position;
            if (spriteAspect > rectAspect)
            {
                float newHeight = position.width / spriteAspect;
                drawRect.y += (position.height - newHeight) / 2f;
                drawRect.height = newHeight;
            }
            else
            {
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

        private void DrawFoldoutSection(string title, ref bool foldout, Color accentColor, System.Action drawContent)
        {
            var headerRect = EditorGUILayout.GetControlRect(false, 26);
            EditorGUI.DrawRect(headerRect, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f));
            
            // Left accent bar
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), accentColor);
            
            // Foldout
            var foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 16, 18);
            foldout = EditorGUI.Foldout(foldoutRect, foldout, title, true, new GUIStyle(EditorStyles.foldout) { 
                fontStyle = FontStyle.Bold,
                fontSize = 11
            });

            if (!foldout) return;

            EditorGUILayout.BeginVertical(sectionStyle);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
        }
    }
}
