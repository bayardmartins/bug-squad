using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Base editor for LocalResource that auto-assigns stable IDs.
    /// IDs are based on position hash to ensure consistency across all clients.
    /// Extend this class for specific resource types (TreeResource, RockResource, etc.)
    /// </summary>
    [CustomEditor(typeof(LocalResource), true)]
    [CanEditMultipleObjects]
    public class LocalResourceEditor : UnityEditor.Editor
    {
        private SerializedProperty resourceIdProp;

        protected virtual void OnEnable()
        {
            resourceIdProp = serializedObject.FindProperty("resourceId");
            
            // Auto-assign ID if missing
            EnsureResourceId();
        }

        /// <summary>
        /// Generates a stable ID based on scene path and position.
        /// This ensures the same ID on all clients since it's baked into the scene.
        /// </summary>
        private void EnsureResourceId()
        {
            serializedObject.Update();

            bool anyChanged = false;
            foreach (var t in targets)
            {
                var resource = t as LocalResource;
                if (resource == null) continue;

                var so = new SerializedObject(resource);
                var idProp = so.FindProperty("resourceId");

                if (idProp.intValue == 0)
                {
                    // Generate stable ID from scene path + hierarchy path + position
                    string scenePath = resource.gameObject.scene.path ?? "prefab";
                    string hierarchyPath = GetHierarchyPath(resource.transform);
                    Vector3 pos = resource.transform.position;

                    // Create stable hash from combined data
                    int hash = 17;
                    hash = hash * 31 + scenePath.GetHashCode();
                    hash = hash * 31 + hierarchyPath.GetHashCode();
                    hash = hash * 31 + Mathf.RoundToInt(pos.x * 100).GetHashCode();
                    hash = hash * 31 + Mathf.RoundToInt(pos.y * 100).GetHashCode();
                    hash = hash * 31 + Mathf.RoundToInt(pos.z * 100).GetHashCode();

                    // Ensure positive non-zero ID
                    if (hash == 0) hash = 1;
                    if (hash < 0) hash = -hash;

                    idProp.intValue = hash;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(resource);
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                serializedObject.Update();
            }
        }

        private string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Show resource ID (read-only)
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(resourceIdProp, new GUIContent("Resource ID (Auto)"));
            EditorGUI.EndDisabledGroup();

            // Button to regenerate ID
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Regenerate ID", GUILayout.Width(100)))
            {
                foreach (var t in targets)
                {
                    var resource = t as LocalResource;
                    if (resource == null) continue;

                    var so = new SerializedObject(resource);
                    var idProp = so.FindProperty("resourceId");
                    idProp.intValue = 0; // Reset to trigger regeneration
                    so.ApplyModifiedProperties();
                }
                EnsureResourceId();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Draw remaining properties (skip resourceId since we drew it)
            DrawPropertiesExcluding(serializedObject, "m_Script", "resourceId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}