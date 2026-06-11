#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom property drawer for SnapCompatibility that shows descriptions for selected rules.
    /// </summary>
    [CustomPropertyDrawer(typeof(SnapCompatibility))]
    public class SnapCompatibilityDrawer : PropertyDrawer
    {
        private const float helpBoxHeight = 40f;
        private const float spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Header + targetType + matchingRule + helpBox + rotationPivot + rotationStep + supportCost
            float height = EditorGUIUtility.singleLineHeight * 6 + spacing * 5;
            height += helpBoxHeight + spacing; // Help box for matching rule
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get properties
            var targetTypeProp = property.FindPropertyRelative("targetType");
            var matchingRuleProp = property.FindPropertyRelative("matchingRule");
            var rotationPivotProp = property.FindPropertyRelative("rotationPivotMode");
            var rotationStepProp = property.FindPropertyRelative("rotationStep");
            var supportCostProp = property.FindPropertyRelative("supportCost");

            float y = position.y;
            float lineHeight = EditorGUIUtility.singleLineHeight;

            // Header/Label
            Rect headerRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.LabelField(headerRect, label, EditorStyles.boldLabel);
            y += lineHeight + spacing;

            // Indent
            EditorGUI.indentLevel++;

            // Target Type
            Rect targetTypeRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(targetTypeRect, targetTypeProp);
            y += lineHeight + spacing;

            // Matching Rule
            Rect matchingRuleRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(matchingRuleRect, matchingRuleProp);
            y += lineHeight + spacing;

            // Matching Rule Description
            Rect matchingHelpRect = new Rect(position.x + 15, y, position.width - 15, helpBoxHeight);
            string matchingDesc = GetMatchingRuleDescription((MatchingRule)matchingRuleProp.enumValueIndex);
            EditorGUI.HelpBox(matchingHelpRect, matchingDesc, MessageType.Info);
            y += helpBoxHeight + spacing;

            // Rotation Pivot Mode
            Rect rotationPivotRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(rotationPivotRect, rotationPivotProp, 
                new GUIContent("Rotation Pivot", "SnapPoint = rotate around connection point. CenterPoint = rotate around piece center."));
            y += lineHeight + spacing;

            // Rotation Step
            Rect rotationStepRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(rotationStepRect, rotationStepProp, 
                new GUIContent("Rotation Step (°)", "Degrees per rotation. 90 = quarter turn, 180 = flip."));
            y += lineHeight + spacing;

            // Support Cost
            Rect supportCostRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(supportCostRect, supportCostProp, 
                new GUIContent("Support Cost", "0 = vertical (same level), 1 = horizontal extension, 2+ = heavy."));

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private string GetMatchingRuleDescription(MatchingRule rule)
        {
            switch (rule)
            {
                case MatchingRule.Face2Face:
                    return "FACE-TO-FACE: Aligns snap point forwards to face OPPOSITE each other. " +
                           "Use for edge-to-edge connections like floor tiles extending side by side.";
                
                case MatchingRule.Face2Object:
                    return "FACE-TO-OBJECT: Aligns OUR snap point's forward to OPPOSE the TARGET BuildPiece's forward direction. " +
                           "Our snap point faces opposite to where the target object is facing.";
                
                case MatchingRule.Object2Face:
                    return "OBJECT-TO-FACE: Aligns OUR BuildPiece's forward to OPPOSE the TARGET snap point's forward direction. " +
                           "Our whole piece faces opposite to where the target snap point is pointing.";
                
                default:
                    return "Unknown matching rule.";
            }
        }
    }
}
#endif
