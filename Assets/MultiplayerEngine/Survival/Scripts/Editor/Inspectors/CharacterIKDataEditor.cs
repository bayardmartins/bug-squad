using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for CharacterIKData.
    /// Overrides standard inspectors to prevent raw coordinate/preset tampering.
    /// Provides statistics, diagnostic checks, a safe preset addition dropdown, and quick links to Dedicated Editors.
    /// </summary>
    [CustomEditor(typeof(CharacterIKData))]
    public class CharacterIKDataEditor : UnityEditor.Editor
    {
        private CharacterIKData data;

        private void OnEnable()
        {
            data = (CharacterIKData)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Header Banner
            MEEditorTheme.DrawHeader("Character IK Profile", "Weapon Alignment, Left/Right Hand Offsets & Hints");

            GUILayout.Space(5);

            // 2. Purpose & Description Card
            MEEditorTheme.BeginCard("System Overview");
            {
                EditorGUILayout.HelpBox(
                    "This ScriptableObject houses bone transform targets and elbows hints offsets used by the Two-Bone IK processor.\n\n" +
                    "⚠ DIRECT MANUAL MODIFICATION OF THE RAW COORDINATE ARRAYS IS PREVENTED here to avoid animator breaks or desynchronized hands. Please use the play-mode Visual Positioners below to edit grip poses.",
                    MessageType.Info
                );

                GUILayout.Space(8);

                string assetPath = AssetDatabase.GetAssetPath(data);
                EditorGUILayout.LabelField("Asset Path:", assetPath, MEEditorTheme.StyleLabelMuted);

                if (File.Exists(assetPath))
                {
                    FileInfo fileInfo = new FileInfo(assetPath);
                    string sizeText = (fileInfo.Length / 1024f).ToString("F2") + " KB";
                    EditorGUILayout.LabelField("IK Configuration Size:", sizeText, MEEditorTheme.StyleLabelMuted);
                }
            }
            MEEditorTheme.EndCard();

            // 3. Database Statistics Card
            MEEditorTheme.BeginCard("Profile Statistics");
            {
                int totalPresets = data.presets != null ? data.presets.Count : 0;
                int activeRightHandPrimaryCount = 0;
                int activeSupportHandIKCount = 0;
                bool hasNullPresets = false;

                if (data.presets != null)
                {
                    for (int i = 0; i < data.presets.Count; i++)
                    {
                        var preset = data.presets[i];
                        if (preset == null)
                        {
                            hasNullPresets = true;
                            continue;
                        }

                        if (preset.isRightHandPrimary)
                        {
                            activeRightHandPrimaryCount++;
                        }
                        if (preset.useSecondaryHand)
                        {
                            activeSupportHandIKCount++;
                        }
                    }
                }

                DrawStatRow("Total Configured Presets:", totalPresets.ToString(), EditorStyles.boldLabel);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawStatRow("👉 Right Hand Primary:", activeRightHandPrimaryCount.ToString(), EditorStyles.label);
                DrawStatRow("👈 Left Hand Primary:", (totalPresets - activeRightHandPrimaryCount).ToString(), EditorStyles.label);
                DrawStatRow("🤝 Dual/Support Grip IK Active:", activeSupportHandIKCount.ToString(), EditorStyles.label);

                if (hasNullPresets)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("Warning: IK Data contains empty/null preset indices!", MessageType.Warning);
                }
            }
            MEEditorTheme.EndCard();



            // 5. Dedicated Editors Card
            MEEditorTheme.BeginCard("Visual Scene Editors");
            {
                GUI.backgroundColor = MEEditorTheme.ColorAccent;
                if (GUILayout.Button("🎯 Open Shooter IK Positioning Window", MEEditorTheme.StylePrimaryButton))
                {
                    ShooterIKEditorWindow.Open();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(6);

                if (GUILayout.Button("👋 Open Grip Hand Pose Positioning Window", MEEditorTheme.StyleSecondaryButton))
                {
                    ItemPoseEditorWindow.Open();
                }

                GUILayout.Space(8);

                if (GUILayout.Button("Find Asset in Project Explorer", MEEditorTheme.StyleSecondaryButton))
                {
                    EditorGUIUtility.PingObject(data);
                }
            }
            MEEditorTheme.EndCard();

            // 6. Footer
            MEEditorTheme.DrawFooter();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatRow(string label, string value, GUIStyle valueStyle)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));
            EditorGUILayout.LabelField(value, valueStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
    }
}