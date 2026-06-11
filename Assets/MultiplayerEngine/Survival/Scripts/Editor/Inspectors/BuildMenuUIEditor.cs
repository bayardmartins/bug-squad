using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for BuildMenuUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes layout validation, color swatch styling, and playmode test tools.
    /// </summary>
    [CustomEditor(typeof(BuildMenuUI))]
    public class BuildMenuUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Radial Build Selector HUD";

        // Serialized properties
        private SerializedProperty buildDatabaseProp;
        private SerializedProperty categorySlotsProp;
        private SerializedProperty itemNameTextProp;
        private SerializedProperty descriptionTextProp;
        private SerializedProperty resource1IconProp;
        private SerializedProperty resource1TextProp;
        private SerializedProperty resource2IconProp;
        private SerializedProperty resource2TextProp;
        private SerializedProperty hoverScaleProp;
        private SerializedProperty normalColorProp;
        private SerializedProperty selectedColorProp;
        private SerializedProperty hoverColorProp;

        private void OnEnable()
        {
            buildDatabaseProp = serializedObject.FindProperty("buildDatabase");
            categorySlotsProp = serializedObject.FindProperty("categorySlots");
            itemNameTextProp = serializedObject.FindProperty("itemNameText");
            descriptionTextProp = serializedObject.FindProperty("descriptionText");
            resource1IconProp = serializedObject.FindProperty("resource1Icon");
            resource1TextProp = serializedObject.FindProperty("resource1Text");
            resource2IconProp = serializedObject.FindProperty("resource2Icon");
            resource2TextProp = serializedObject.FindProperty("resource2Text");
            hoverScaleProp = serializedObject.FindProperty("hoverScale");
            normalColorProp = serializedObject.FindProperty("normalColor");
            selectedColorProp = serializedObject.FindProperty("selectedColor");
            hoverColorProp = serializedObject.FindProperty("hoverColor");
        }

        protected override void DrawInspectorBody()
        {
            DrawMessage("Attached to the Radial Build Wheel HUD canvas to select and cycle through category build pieces.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Data Registry ──
            BeginCard("Build Database Configuration");
            {
                DrawProperty(buildDatabaseProp, "Build Database Registry", "The central scriptable database containing all catalog building pieces.");
                
                if (buildDatabaseProp.objectReferenceValue == null)
                {
                    DrawMessage("Build Database is unassigned! Build menu categories cannot initialize.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 2: UI Slots Setup ──
            BeginCard("UI Category Slots (6 Fixed)");
            {
                DrawProperty(categorySlotsProp, "Category Slots Grid", "The array containing exactly 6 UI slot items linked to the circular overlay.");

                int assignedCount = 0;
                for (int i = 0; i < categorySlotsProp.arraySize; i++)
                {
                    if (categorySlotsProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                        assignedCount++;
                }

                GUILayout.Label($"<b>Slot Assignment Status</b>: {assignedCount} / 6 assigned", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                if (assignedCount < 6)
                {
                    DrawMessage("Fewer than 6 slots are assigned. The radial layout may render with missing categories.", MessageType.Warning);
                }
            }
            EndCard();

            // ── Card 3: Info HUD Panel ──
            BeginCard("Central Description HUD");
            {
                DrawProperty(itemNameTextProp, "Name Label Text", "TMP Text field presenting the selected piece name.");
                DrawProperty(descriptionTextProp, "Description Details", "TMP Text field presenting structural stability warnings/uses.");
                
                GUILayout.Space(4);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(4);

                GUILayout.Label("<b>Required Material Swatches</b>", EditorStyles.miniBoldLabel);
                DrawProperty(resource1IconProp, "Cost Icon 1 Image", "UI Image displaying item icon for primary resource costs.");
                DrawProperty(resource1TextProp, "Cost Counter 1 Text", "TMP Text displaying total available inventory vs needed primary resource amount.");
                DrawProperty(resource2IconProp, "Cost Icon 2 Image", "UI Image displaying item icon for secondary resource costs.");
                DrawProperty(resource2TextProp, "Cost Counter 2 Text", "TMP Text displaying total available inventory vs needed secondary resource amount.");
            }
            EndCard();

            // ── Card 4: Aesthetics & Theme Styling ──
            BeginCard("UI Hover & Palette Styles");
            {
                DrawProperty(hoverScaleProp, "Hover Scale Factor", "Physical size multiplayer applied when hovering crosshairs over a category slot (default: 1.15).");

                GUILayout.Space(6);
                
                // Color field configurations
                DrawProperty(normalColorProp, "Normal Overlay Color", "Base color tint applied to idle radial slices.");
                DrawProperty(selectedColorProp, "Selected Accent Color", "Accent color tint highlighting the active category slice.");
                DrawProperty(hoverColorProp, "Hover Glow Color", "Transient color tint applied when mouse pointer hovers over a slice.");

                GUILayout.Space(8);
                GUILayout.Label("<b>Active Color Palette Preview</b>", EditorStyles.miniBoldLabel);
                
                // Draw custom premium palette swatch block
                GUILayout.BeginHorizontal();
                {
                    DrawColorSwatch("Normal", normalColorProp.colorValue);
                    GUILayout.Space(10);
                    DrawColorSwatch("Selected", selectedColorProp.colorValue);
                    GUILayout.Space(10);
                    DrawColorSwatch("Hovered", hoverColorProp.colorValue);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            EndCard();

            // ── Playmode Live Debugger ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        private void DrawColorSwatch(string label, Color col)
        {
            GUILayout.BeginVertical(GUILayout.Width(60));
            {
                GUILayout.Label(label, EditorStyles.miniLabel);
                Rect rect = GUILayoutUtility.GetRect(60, 16);
                EditorGUI.DrawRect(rect, col);
                // Draw border
                Handles.DrawSolidRectangleWithOutline(rect, col, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            }
            GUILayout.EndVertical();
        }

        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Radial Menu Debugger");
            {
                var ui = (BuildMenuUI)target;

                // Live States
                GUILayout.Label($"<b>Menu Visibility</b>: {(ui.IsOpen ? "<color=#66CD00>OPEN</color>" : "<color=#CD2626>CLOSED</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                var selectedPiece = ui.SelectedPiece;
                if (selectedPiece != null)
                {
                    GUILayout.Label($"<b>Selected Piece</b>: <color=#66CD00>{selectedPiece.pieceName}</color>", new GUIStyle(EditorStyles.label) { richText = true });
                }
                else
                {
                    GUILayout.Label("<b>Selected Piece</b>: <i>None (Category not active)</i>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(10);

                // Simulation Buttons
                GUILayout.Label("<b>UI Visual Testing Binds</b>", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Toggle Wheel Visibility", MEEditorTheme.StylePrimaryButton))
                    {
                        ui.Toggle();
                        Debug.Log("[BuildMenuUIEditor] Toggled radial selection wheel visibility.");
                    }

                    if (ui.IsOpen)
                    {
                        if (GUILayout.Button("Force Close HUD", MEEditorTheme.StyleSecondaryButton))
                        {
                            ui.Close();
                            Debug.Log("[BuildMenuUIEditor] Circular overlay closed.");
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Force Open HUD", MEEditorTheme.StyleSecondaryButton))
                        {
                            ui.Open();
                            Debug.Log("[BuildMenuUIEditor] Circular overlay opened.");
                        }
                    }
                }
                GUILayout.EndHorizontal();

                if (ui.IsOpen)
                {
                    GUILayout.Space(6);
                    GUILayout.Label("<b>Trigger category selection directly:</b>", EditorStyles.miniLabel);
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < 6; i++)
                    {
                        if (GUILayout.Button($"Slot {i + 1}", GUILayout.Width(50)))
                        {
                            ui.SelectSlot(i);
                            Debug.Log($"[BuildMenuUIEditor] Active category set to Slot {i + 1}.");
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                Repaint();
            }
            EndCard();
        }
    }
}
