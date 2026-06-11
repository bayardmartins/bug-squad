using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Scene-view Item Pose Editor.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// Adjusts interactive Position/Rotation handles for hand offset (grip adjustment) for equipped items.
    /// </summary>
    public class ItemPoseEditorWindow : MEEditorWindow
    {
        private enum EditTool { Position, Rotation }

        // State
        private EditTool editTool = EditTool.Position;

        // References
        private EquipmentController equipController;

        // Cached info
        private string itemName = "No Item";

        // Colors
        private static readonly Color HandOffsetColor = new Color(0.85f, 0.55f, 0.15f, 0.9f);

        protected override bool UseGlobalScrollView => true;
        protected override string WindowSubtitle => "Equipped Item Grip Position & Rotation Offset";

        [MenuItem("Tools/Multiplayer Engine/Item Pose Editor", false, 22)]
        public static void Open()
        {
            var w = GetWindow<ItemPoseEditorWindow>();
            w.titleContent = new GUIContent("Item Pose Editor", EditorGUIUtility.IconContent("d_AvatarPivot").image);
            w.minSize = new Vector2(350, 480);
            w.Show();
        }

        public static void Open(EquipmentController controller)
        {
            var w = GetWindow<ItemPoseEditorWindow>();
            w.titleContent = new GUIContent("Item Pose Editor", EditorGUIUtility.IconContent("d_AvatarPivot").image);
            w.minSize = new Vector2(350, 480);
            w.equipController = controller;
            w.Refresh();
            w.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            titleContent = new GUIContent("Item Pose Editor", EditorGUIUtility.IconContent("d_AvatarPivot").image);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void Refresh()
        {
            if (equipController == null)
                equipController = Object.FindFirstObjectByType<EquipmentController>();

            if (equipController != null)
            {
                var currentItem = equipController.GetCurrentItemData();
                if (currentItem != null)
                {
                    itemName = currentItem.itemName;
                }
                else
                {
                    itemName = "No Item Equipped";
                }
            }
        }

        protected override void DrawBody()
        {
            if (!Application.isPlaying)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "Enter Play Mode and equip an item in-game to start editing grip offsets visually.",
                    MessageType.Info);
                return;
            }

            Refresh();

            if (equipController == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("No EquipmentController found in scene.", MessageType.Warning);
                return;
            }

            DrawItemInfo();
            DrawToolButtons();

            GUILayout.Space(10);
            DrawCurrentValues();

            GUILayout.Space(10);
            DrawSaveButton();

            Repaint();
            SceneView.RepaintAll();
        }

        private void DrawItemInfo()
        {
            MEEditorTheme.BeginCard("Current Equipped Item");
            
            EditorGUILayout.LabelField("Item Name:", itemName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Adjustment Target: Hand Grip Offset", EditorStyles.miniLabel);
            
            MEEditorTheme.EndCard();
        }

        private void DrawToolButtons()
        {
            MEEditorTheme.BeginCard("Select Adjustment Gizmo");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Position Offset Tool", editTool == EditTool.Position ? MEEditorTheme.StylePrimaryButton : MEEditorTheme.StyleSecondaryButton, GUILayout.Height(30))) 
                editTool = EditTool.Position;

            if (GUILayout.Button("Rotation Offset Tool", editTool == EditTool.Rotation ? MEEditorTheme.StylePrimaryButton : MEEditorTheme.StyleSecondaryButton, GUILayout.Height(30))) 
                editTool = EditTool.Rotation;

            EditorGUILayout.EndHorizontal();

            MEEditorTheme.EndCard();
        }

        private void DrawCurrentValues()
        {
            Transform model = GetEquippedModelTransform();
            if (model == null) return;

            MEEditorTheme.BeginCard("Relative Local Transform");
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector3Field("Local Position Offset", model.localPosition);
            EditorGUILayout.Vector3Field("Local Rotation (Euler)", model.localRotation.eulerAngles);
            EditorGUI.EndDisabledGroup();

            MEEditorTheme.EndCard();
        }

        private void DrawSaveButton()
        {
            MEEditorTheme.BeginCard("Database Persistent Storage");

            if (GUILayout.Button("SAVE Item Grip Offset", MEEditorTheme.StylePrimaryButton, GUILayout.Height(36)))
            {
                SaveHandOffset();
            }
            
            MEEditorTheme.EndCard();
        }

        private Transform GetEquippedModelTransform()
        {
            if (equipController == null) return null;
            var handler = equipController.GetCurrentHandler<BaseItemHandler>();
            return handler?.EquippedObject?.transform;
        }

        #region Scene Handles

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;

            DrawHandOffsetHandle();
            DrawOverlay(sceneView);
        }

        private void DrawHandOffsetHandle()
        {
            Transform t = GetEquippedModelTransform();
            if (t == null) return;

            Handles.color = HandOffsetColor;
            Handles.Label(t.position + Vector3.up * 0.05f,
                editTool == EditTool.Position ? "Hand Offset [POS]" : "Hand Offset [ROT]",
                GetLabelStyle(HandOffsetColor));

            if (editTool == EditTool.Position)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion handleRot = t.parent != null ? t.parent.rotation : Quaternion.identity;
                Vector3 newPos = Handles.PositionHandle(t.position, handleRot);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "Move Hand Offset");
                    t.position = newPos;
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                Quaternion newRot = Handles.RotationHandle(t.rotation, t.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "Rotate Hand Offset");
                    t.rotation = newRot;
                }
            }
        }

        private void DrawOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();
            float w = 220f, h = 60f;
            Rect r = new Rect(10, sceneView.position.height - h - 30, w, h);
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(new Rect(r.x + 5, r.y + 5, w - 10, h - 10));
            GUILayout.Label("Editing: Base Hand Offset", EditorStyles.boldLabel);
            GUILayout.Label($"Gizmo: {(editTool == EditTool.Position ? "Position" : "Rotation")}", EditorStyles.miniLabel);

            if (GUILayout.Button("Save Hand Offset", MEEditorTheme.StylePrimaryButton, GUILayout.Height(18)))
                SaveHandOffset();

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private GUIStyle GetLabelStyle(Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        #endregion

        #region Save

        private void SaveHandOffset()
        {
            Transform model = GetEquippedModelTransform();
            if (model == null) return;

            HandOffsetData offsetData = GetActiveHandOffsetData();
            if (offsetData == null)
            {
                Debug.LogWarning("[ItemPoseEditor] No HandOffsetData asset assigned to this item.");
                return;
            }

            Undo.RecordObject(offsetData, "Save Hand Offset");
            offsetData.positionOffset = model.localPosition;
            offsetData.rotationOffset = model.localRotation.eulerAngles;
            EditorUtility.SetDirty(offsetData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemPoseEditor] Saved Hand Offset to {offsetData.name}");
        }

        private HandOffsetData GetActiveHandOffsetData()
        {
            if (equipController == null) return null;

            var itemData = equipController.GetCurrentItemData();
            return itemData?.handOffsetData;
        }

        #endregion
    }
}