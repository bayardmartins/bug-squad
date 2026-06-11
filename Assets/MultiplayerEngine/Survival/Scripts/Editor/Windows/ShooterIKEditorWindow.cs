using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Dedicated Item Pose Editor for Shooter Weapons using Two-Bone IK.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// Allows visual adjustment of main and off-hand IK targets and elbow hints,
    /// storing the offsets directly into WeaponIKPreset data.
    /// </summary>
    public class ShooterIKEditorWindow : MEEditorWindow
    {
        private enum EditTarget { LeftHand, RightHand, LeftHint, RightHint }
        private enum PoseMode { Standing, Aiming }
        private enum HandleMode { Position, Rotation }

        // State
        private EditTarget editTarget = EditTarget.LeftHand;
        private PoseMode poseMode = PoseMode.Standing;
        private HandleMode handleMode = HandleMode.Position;
        
        // References
        private EquipmentController equipController;
        private ShooterIKController ikController;

        // UI State
        private bool isLeftWeapon;
        
        // Colors for Scene Handle visuals
        private static readonly Color PrimaryColor = new Color(0.33f, 0.41f, 0.92f, 0.9f);
        private static readonly Color SecondaryColor = new Color(0.0f, 0.7f, 0.9f, 0.9f);
        private static readonly Color PrimaryElbowColor = new Color(0.85f, 0.55f, 0.15f, 0.9f);
        private static readonly Color SecondaryElbowColor = new Color(0.15f, 0.60f, 0.35f, 0.9f);

        protected override bool UseGlobalScrollView => true;
        protected override string WindowSubtitle => "Visual Two-Bone Weapon IK Positioner";

        [MenuItem("Tools/Multiplayer Engine/Shooter IK Editor", false, 23)]
        public static void Open()
        {
            var w = GetWindow<ShooterIKEditorWindow>();
            w.titleContent = new GUIContent("Shooter IK", EditorGUIUtility.IconContent("d_KinematicBody").image);
            w.minSize = new Vector2(350, 520);
            w.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            titleContent = new GUIContent("Shooter IK", EditorGUIUtility.IconContent("d_KinematicBody").image);
        }

        private void OnDisable()
        {
            if (ikController != null) ikController.EditorForceAim = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void Refresh()
        {
            if (equipController == null)
                equipController = Object.FindFirstObjectByType<EquipmentController>();

            if (equipController != null)
            {
                ikController = equipController.GetComponentInChildren<ShooterIKController>();
            }
        }

        protected override void DrawBody()
        {
            if (!Application.isPlaying)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Enter Play Mode and equip a shooter weapon to use the IK Adjust Editor.", MessageType.Info);
                return;
            }

            Refresh();
            
            if (ikController == null || !ikController.IsActive || ikController.CurrentPreset == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("No active ShooterIKController or WeaponIKPreset found. Please equip a shooter weapon in-game.", MessageType.Warning);
                return;
            }

            SyncStates();

            DrawPresetInfo();
            DrawPoseMode();
            DrawEditTargetButtons();
            
            GUILayout.Space(10);
            DrawSpineSettings();

            GUILayout.Space(10);
            DrawDiagnostics();

            GUILayout.Space(10);
            DrawSaveButton();

            Repaint();
            SceneView.RepaintAll();
        }

        private void SyncStates()
        {
            isLeftWeapon = !ikController.CurrentPreset.isRightHandPrimary;
            if (poseMode == PoseMode.Aiming && !ikController.EditorForceAim) ikController.EditorForceAim = true;
            if (poseMode == PoseMode.Standing && ikController.EditorForceAim) ikController.EditorForceAim = false;
        }

        private void DrawPresetInfo()
        {
            MEEditorTheme.BeginCard("Active IK Profile");

            string presetNameText = "None Assigned";
            if (ikController.CurrentPreset.itemData != null)
            {
                presetNameText = ikController.CurrentPreset.itemData.itemName;
            }
            
            EditorGUILayout.LabelField("Assigned Weapon:", presetNameText, EditorStyles.boldLabel);
            GUILayout.Space(8);

            // Editable: Primary Hand toggle
            EditorGUI.BeginChangeCheck();
            bool isRight = EditorGUILayout.Toggle("Right Hand Primary", ikController.CurrentPreset.isRightHandPrimary);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ikController.CharacterIKData, "Change Primary Hand");
                ikController.CurrentPreset.isRightHandPrimary = isRight;
                EditorUtility.SetDirty(ikController.CharacterIKData);
            }
            EditorGUILayout.LabelField(isRight ? "Weapon in RIGHT hand, support in LEFT" : "Weapon in LEFT hand, support in RIGHT", EditorStyles.miniLabel);

            GUILayout.Space(5);

            // Editable: Use Secondary Hand toggle
            EditorGUI.BeginChangeCheck();
            bool useSecondary = EditorGUILayout.Toggle("Use Secondary Hand IK", ikController.CurrentPreset.useSecondaryHand);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ikController.CharacterIKData, "Change Secondary Hand");
                ikController.CurrentPreset.useSecondaryHand = useSecondary;
                EditorUtility.SetDirty(ikController.CharacterIKData);
            }

            MEEditorTheme.EndCard();

            // Equip Blend Duration
            MEEditorTheme.BeginCard("Weapon Transition Settings");

            EditorGUI.BeginChangeCheck();
            float blendDuration = EditorGUILayout.Slider("Equip Blend In", ikController.equipBlendDuration, 0f, 2f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ikController, "Change Equip Blend Duration");
                ikController.equipBlendDuration = blendDuration;
            }
            EditorGUILayout.LabelField("IK fade-in after equipping weapon", EditorStyles.miniLabel);

            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            float blendOut = EditorGUILayout.Slider("Unequip Blend Out", ikController.unequipBlendDuration, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ikController, "Change Unequip Blend Duration");
                ikController.unequipBlendDuration = blendOut;
            }
            EditorGUILayout.LabelField("IK fade-out when swap animation starts", EditorStyles.miniLabel);

            MEEditorTheme.EndCard();
        }

        private void DrawPoseMode()
        {
            MEEditorTheme.BeginCard("Preview & Edit State");
            
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Standing (Idle)", poseMode == PoseMode.Standing ? MEEditorTheme.StylePrimaryButton : MEEditorTheme.StyleSecondaryButton, GUILayout.Height(30))) 
                poseMode = PoseMode.Standing;

            if (GUILayout.Button("Aiming (Sight)", poseMode == PoseMode.Aiming ? MEEditorTheme.StylePrimaryButton : MEEditorTheme.StyleSecondaryButton, GUILayout.Height(30))) 
                poseMode = PoseMode.Aiming;

            EditorGUILayout.EndHorizontal();
            
            MEEditorTheme.EndCard();
        }

        private void DrawEditTargetButtons()
        {
            MEEditorTheme.BeginCard("Select Joint to Manipulate");

            // Hand buttons
            EditorGUILayout.BeginHorizontal();
            if (editTarget == EditTarget.LeftHand)
            {
                GUI.backgroundColor = PrimaryColor;
                if (GUILayout.Button("Left Hand", MEEditorTheme.StyleDynamicButton, GUILayout.Height(32))) editTarget = EditTarget.LeftHand;
            }
            else
            {
                if (GUILayout.Button("Left Hand", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(32))) editTarget = EditTarget.LeftHand;
            }

            if (editTarget == EditTarget.RightHand)
            {
                GUI.backgroundColor = SecondaryColor;
                if (GUILayout.Button("Right Hand", MEEditorTheme.StyleDynamicButton, GUILayout.Height(32))) editTarget = EditTarget.RightHand;
            }
            else
            {
                if (GUILayout.Button("Right Hand", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(32))) editTarget = EditTarget.RightHand;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Elbow buttons
            EditorGUILayout.BeginHorizontal();
            if (editTarget == EditTarget.LeftHint)
            {
                GUI.backgroundColor = PrimaryElbowColor;
                if (GUILayout.Button("Left Elbow Hint", MEEditorTheme.StyleDynamicButton, GUILayout.Height(28))) editTarget = EditTarget.LeftHint;
            }
            else
            {
                if (GUILayout.Button("Left Elbow Hint", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(28))) editTarget = EditTarget.LeftHint;
            }

            if (editTarget == EditTarget.RightHint)
            {
                GUI.backgroundColor = SecondaryElbowColor;
                if (GUILayout.Button("Right Elbow Hint", MEEditorTheme.StyleDynamicButton, GUILayout.Height(28))) editTarget = EditTarget.RightHint;
            }
            else
            {
                if (GUILayout.Button("Right Elbow Hint", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(28))) editTarget = EditTarget.RightHint;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            MEEditorTheme.DrawDivider();
            GUILayout.Space(10);

            // Handle mode toggle (Position / Rotation)
            EditorGUILayout.BeginHorizontal();
            if (handleMode == HandleMode.Position)
            {
                GUI.backgroundColor = MEEditorTheme.ColorSuccess;
                if (GUILayout.Button("Position Handle", MEEditorTheme.StyleDynamicButton, GUILayout.Height(26))) handleMode = HandleMode.Position;
            }
            else
            {
                if (GUILayout.Button("Position Handle", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(26))) handleMode = HandleMode.Position;
            }

            if (handleMode == HandleMode.Rotation)
            {
                GUI.backgroundColor = MEEditorTheme.ColorSuccess;
                if (GUILayout.Button("Rotation Handle", MEEditorTheme.StyleDynamicButton, GUILayout.Height(26))) handleMode = HandleMode.Rotation;
            }
            else
            {
                if (GUILayout.Button("Rotation Handle", MEEditorTheme.StyleSecondaryButton, GUILayout.Height(26))) handleMode = HandleMode.Rotation;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            
            if (GUILayout.Button("Reset Selection to Zero Offset", MEEditorTheme.StyleSecondaryButton))
            {
                IKOffsetTransform activeOffset = GetActiveIKOffset(out _);
                if (activeOffset != null)
                {
                    Undo.RecordObject(ikController.CharacterIKData, "Reset IK Offset");
                    activeOffset.position = Vector3.zero;
                    activeOffset.eulerAngles = Vector3.zero;
                    EditorUtility.SetDirty(ikController.CharacterIKData);
                }
            }
            
            MEEditorTheme.EndCard();
        }

        private void DrawSpineSettings()
        {
            IKAdjust activeAdjust = poseMode == PoseMode.Standing ? ikController.CurrentPreset.idle : ikController.CurrentPreset.aiming;

            MEEditorTheme.BeginCard("Additive Spinal & Head Tilt Offset");
            
            EditorGUI.BeginChangeCheck();
            Vector3 spineRot = EditorGUILayout.Vector3Field("Spine Offset (Euler)", activeAdjust.spineOffset);
            Vector3 headRot = EditorGUILayout.Vector3Field("Head Offset (Euler)", activeAdjust.headOffset);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ikController.CharacterIKData, "Change Spine Settings");
                activeAdjust.spineOffset = spineRot;
                activeAdjust.headOffset = headRot;
                EditorUtility.SetDirty(ikController.CharacterIKData);
            }
            
            MEEditorTheme.EndCard();
        }

        private void DrawDiagnostics()
        {
            MEEditorTheme.BeginCard("Support Hand Diagnostics");

            bool hasGrip = ikController.SupportHandTarget != null;
            bool useSecondary = ikController.CurrentPreset.useSecondaryHand;

            if (!useSecondary)
            {
                EditorGUILayout.HelpBox("Secondary Hand IK is disabled in the profile preset.", MessageType.Warning);
            }
            else if (!hasGrip)
            {
                EditorGUILayout.HelpBox("Missing 'SecondaryHandGrip' GameObject on active weapon prefab!", MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField("Grip Hook Found:", "Operational", EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField("Support IK Weight:", ikController.SupportIKWeight.ToString("F3"));
            EditorGUILayout.LabelField("Master Rig Weight:", ikController.MasterWeight.ToString("F3"));

            IKAdjust activeAdjust = poseMode == PoseMode.Standing ? ikController.CurrentPreset.idle : ikController.CurrentPreset.aiming;
            EditorGUILayout.LabelField("Support Pos Offset:", activeAdjust.secondaryHandOffset.position.ToString("F3"));
            EditorGUILayout.LabelField("Support Rot Offset:", activeAdjust.secondaryHandOffset.eulerAngles.ToString("F1"));

            MEEditorTheme.EndCard();
        }

        private void DrawSaveButton()
        {
            MEEditorTheme.BeginCard("Database Persistent Storage");
            
            if (GUILayout.Button("SAVE IK Preset Config Data", MEEditorTheme.StylePrimaryButton, GUILayout.Height(36)))
            {
                if (ikController != null && ikController.CharacterIKData != null)
                {
                    Undo.RecordObject(ikController.CharacterIKData, "Save IK Preset");
                    EditorUtility.SetDirty(ikController.CharacterIKData);
                    AssetDatabase.SaveAssets();

                    string presetNameText = "Unnamed";
                    if (ikController.CurrentPreset.itemData != null)
                    {
                        presetNameText = ikController.CurrentPreset.itemData.itemName;
                    }
                    
                    Debug.Log($"[ShooterIKEditor] Saved Weapon IK Preset changes for: {presetNameText}");
                }
            }
            
            MEEditorTheme.EndCard();
        }

        #region Scene View logic

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying || ikController == null || !ikController.IsActive || ikController.CurrentPreset == null)
                return;

            DrawArmLines();

            IKOffsetTransform activeOffset = GetActiveIKOffset(out Transform referenceBone);
            if (activeOffset != null && referenceBone != null)
            {
                DrawTransformHandles(activeOffset, referenceBone);
            }
            
            DrawOverlay(sceneView);
        }

        private void DrawArmLines()
        {
            if (ikController.LeftIK != null && ikController.LeftIK.IsValid)
            {
                Handles.color = PrimaryColor;
                Handles.DrawAAPolyLine(
                    ikController.LeftIK.rootBone.position, 
                    ikController.LeftIK.middleBone.position, 
                    ikController.LeftIK.endBone.position
                );
            }
            
            if (ikController.RightIK != null && ikController.RightIK.IsValid)
            {
                Handles.color = SecondaryColor;
                Handles.DrawAAPolyLine(
                    ikController.RightIK.rootBone.position, 
                    ikController.RightIK.middleBone.position, 
                    ikController.RightIK.endBone.position
                );
            }

            if (ikController.SupportHandTarget != null)
            {
                Handles.color = Color.magenta;
                Handles.DrawWireCube(ikController.SupportHandTarget.position, Vector3.one * 0.025f);
                var s = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.magenta } };
                Handles.Label(ikController.SupportHandTarget.position + Vector3.up * 0.06f, "Grip Target", s);
            }
        }

        private IKOffsetTransform GetActiveIKOffset(out Transform referenceBone)
        {
            referenceBone = null;
            if (ikController == null || ikController.CurrentPreset == null) return null;
            
            IKAdjust activeAdjust = poseMode == PoseMode.Standing ? ikController.CurrentPreset.idle : ikController.CurrentPreset.aiming;
            
            bool isLeftPrimary = !ikController.CurrentPreset.isRightHandPrimary;

            if (editTarget == EditTarget.LeftHand)
            {
                if (isLeftPrimary)
                {
                    referenceBone = ikController.LeftIK?.endBoneRef;
                    return activeAdjust.primaryHandOffset;
                }
                else
                {
                    referenceBone = ikController.SupportHandTarget != null ? ikController.SupportHandTarget : ikController.LeftIK?.endBoneRef;
                    return activeAdjust.secondaryHandOffset;
                }
            }
            if (editTarget == EditTarget.RightHand)
            {
                if (!isLeftPrimary)
                {
                    referenceBone = ikController.RightIK?.endBoneRef;
                    return activeAdjust.primaryHandOffset;
                }
                else
                {
                    referenceBone = ikController.SupportHandTarget != null ? ikController.SupportHandTarget : ikController.RightIK?.endBoneRef;
                    return activeAdjust.secondaryHandOffset;
                }
            }
            if (editTarget == EditTarget.LeftHint)
            {
                referenceBone = ikController.LeftIK?.middleBoneRef;
                return isLeftPrimary ? activeAdjust.primaryHintOffset : activeAdjust.secondaryHintOffset;
            }
            if (editTarget == EditTarget.RightHint)
            {
                referenceBone = ikController.RightIK?.middleBoneRef;
                return isLeftPrimary ? activeAdjust.secondaryHintOffset : activeAdjust.primaryHintOffset;
            }

            return null;
        }

        private void DrawTransformHandles(IKOffsetTransform offset, Transform reference)
        {
            if (offset == null || reference == null) return;

            Vector3 worldPos = reference.TransformPoint(offset.position);
            Quaternion worldRot = reference.rotation * Quaternion.Euler(offset.eulerAngles);

            Color c = Color.white;
            if (editTarget == EditTarget.LeftHand) c = PrimaryColor;
            else if (editTarget == EditTarget.RightHand) c = SecondaryColor;
            else if (editTarget == EditTarget.LeftHint) c = PrimaryElbowColor;
            else if (editTarget == EditTarget.RightHint) c = SecondaryElbowColor;

            Handles.color = c;
            GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
            s.normal.textColor = c;
            Handles.Label(worldPos + Vector3.up * 0.1f, editTarget.ToString(), s);

            if (handleMode == HandleMode.Position)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(worldPos, Tools.pivotRotation == PivotRotation.Local ? worldRot : Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ikController.CharacterIKData, "Move IK Offset");
                    offset.position = reference.InverseTransformPoint(newPos);
                    EditorUtility.SetDirty(ikController.CharacterIKData);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                Quaternion newRot = Handles.RotationHandle(worldRot, worldPos);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ikController.CharacterIKData, "Rotate IK Offset");
                    offset.eulerAngles = (Quaternion.Inverse(reference.rotation) * newRot).eulerAngles;
                    EditorUtility.SetDirty(ikController.CharacterIKData);
                }
            }
        }

        private void DrawOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();
            float w = 240f, h = 60f;
            Rect r = new Rect(10, sceneView.position.height - h - 30, w, h);
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(new Rect(r.x + 5, r.y + 5, w - 10, h - 10));
            GUILayout.Label($"IK Tool: {editTarget}", EditorStyles.boldLabel);
            GUILayout.Label($"State: {poseMode} | Mode: {handleMode}", EditorStyles.miniLabel);

            if (GUILayout.Button("Save IK Config", MEEditorTheme.StylePrimaryButton, GUILayout.Height(18)))
            {
                if (ikController.CharacterIKData != null)
                {
                    EditorUtility.SetDirty(ikController.CharacterIKData);
                    AssetDatabase.SaveAssets();
                }
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        #endregion
    }
}