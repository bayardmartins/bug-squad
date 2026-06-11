#if UNITY_EDITOR && (UNITY_SERVICES || STEAM_SERVICES)
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine.Editor
{
    [CustomEditor(typeof(DamageSource))]
    public class DamageSourceEditor : UnityEditor.Editor
    {
        SerializedProperty damage;
        SerializedProperty damageType;
        SerializedProperty mode;
        SerializedProperty tickInterval;
        SerializedProperty hitLayers;

        private void OnEnable()
        {
            damage = serializedObject.FindProperty("damage");
            damageType = serializedObject.FindProperty("damageType");
            mode = serializedObject.FindProperty("mode");
            tickInterval = serializedObject.FindProperty("tickInterval");
            hitLayers = serializedObject.FindProperty("hitLayers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Damage Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(damageType);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mode);

            // Show tick interval only for OverTime mode
            if ((DamageMode)mode.enumValueIndex == DamageMode.OverTime)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(tickInterval);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitLayers);

            // Validation help box
            var source = (DamageSource)target;
            var collider = source.GetComponent<Collider>();
            if (collider == null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "This GameObject needs a Collider component for damage detection.\n" +
                    "• Instant mode: Works with both regular Colliders and Trigger Colliders.\n" +
                    "• OverTime mode: Requires a Trigger Collider (Is Trigger = true).",
                    MessageType.Warning);
            }
            else if ((DamageMode)mode.enumValueIndex == DamageMode.OverTime && !collider.isTrigger)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "OverTime mode requires the Collider to have 'Is Trigger' enabled.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
