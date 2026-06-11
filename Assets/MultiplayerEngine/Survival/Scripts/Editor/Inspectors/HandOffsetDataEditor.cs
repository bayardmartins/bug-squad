using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Inspector Editor for HandOffsetData ScriptableObject.
    /// Exposes fine-tuning keyboard controls for offsets alongside a button to launch the visual positioner tool.
    /// </summary>
    [CustomEditor(typeof(HandOffsetData))]
    public class HandOffsetDataEditor : UnityEditor.Editor
    {
        private HandOffsetData offsetData;
        private SerializedProperty positionOffsetProp;
        private SerializedProperty rotationOffsetProp;

        private void OnEnable()
        {
            offsetData = (HandOffsetData)target;
            positionOffsetProp = serializedObject.FindProperty("positionOffset");
            rotationOffsetProp = serializedObject.FindProperty("rotationOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Header Banner
            MEEditorTheme.DrawHeader("Hand Grip Offset", "Equipped Weapon & Tool Position & Rotation Offsets");

            GUILayout.Space(5);

            // 2. Purpose Card
            MEEditorTheme.BeginCard("System Overview");
            {
                EditorGUILayout.HelpBox(
                    "This ScriptableObject stores the transform offset between the character skeleton hand anchor and the item grip pivot.\n\n" +
                    "✍ ADVANCED FINE-TUNING is enabled below, but visual scene-view positioning is highly recommended using the Item Pose Editor in Play Mode.",
                    MessageType.Info
                );
            }
            MEEditorTheme.EndCard();

            // 3. Local Offset Coordinates Card
            MEEditorTheme.BeginCard("Relative Transform Vectors");
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(positionOffsetProp, new GUIContent("Position Offset", "Local coordinate position offset from the hand anchor."));
                EditorGUILayout.PropertyField(rotationOffsetProp, new GUIContent("Rotation Offset (Euler)", "Local euler rotation angles offset (degrees)."));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(offsetData);
                }
            }
            MEEditorTheme.EndCard();

            // 4. Primary Editor Controls Card
            MEEditorTheme.BeginCard("Visual Positioner Actions");
            {
                GUI.backgroundColor = MEEditorTheme.ColorAccent;
                if (GUILayout.Button("🎬 Open Play-Mode Item Pose Editor", MEEditorTheme.StylePrimaryButton))
                {
                    ItemPoseEditorWindow.Open();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(8);

                if (GUILayout.Button("Find Asset in Project Explorer", MEEditorTheme.StyleSecondaryButton))
                {
                    EditorGUIUtility.PingObject(offsetData);
                }
            }
            MEEditorTheme.EndCard();

            // 5. Standard Footer branding
            MEEditorTheme.DrawFooter();
        }
    }
}
