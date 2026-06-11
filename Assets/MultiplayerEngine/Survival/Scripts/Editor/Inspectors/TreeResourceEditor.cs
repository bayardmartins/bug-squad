using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for TreeResource with 3-slot model system.
    /// Shows dropdown for selecting drop item from ItemDatabase.
    /// Extends LocalResourceEditor for auto-ID assignment.
    /// </summary>
    [CustomEditor(typeof(TreeResource))]
    [CanEditMultipleObjects]
    public class TreeResourceEditor : LocalResourceEditor
    {
        // Drop config properties
        private SerializedProperty dropConfigProp;
        private SerializedProperty requiredTierProp;
        private SerializedProperty maxHealthProp;

        // Tree Model Slots (3-slot system)
        private SerializedProperty fullTreeSlotProp;
        private SerializedProperty upperPartSlotProp;
        private SerializedProperty stumpSlotProp;

        // Hit effects
        private SerializedProperty hitVFXProp;
        private SerializedProperty hitSoundProp;
        private SerializedProperty shakeIntensityProp;
        private SerializedProperty shakeDurationProp;

        // Fall settings
        private SerializedProperty fallPushForceProp;
        private SerializedProperty forcePointOffsetProp;
        private SerializedProperty fallDirectionProp;
        private SerializedProperty fallSoundProp;
        private SerializedProperty fallImpactVFXProp;

        // Gizmo Settings
        private SerializedProperty showForceGizmoProp;
        private SerializedProperty gizmoSphereRadiusProp;
        private SerializedProperty gizmoArrowLengthProp;

        // Ground Detection
        private SerializedProperty impactDecelerationThresholdProp;

        // Drop Spawn Points
        private SerializedProperty dropSpawnPointsProp;

        // After Fall
        private SerializedProperty destroyDelayProp;
        private SerializedProperty stumpLifetimeProp;


        // Database cache
        private static ItemDatabase cachedDatabase;
        private static string[] itemNames;
        private static InventoryItemData[] itemDataArray;

        protected override void OnEnable()
        {
            base.OnEnable(); // This handles auto-ID assignment
            
            dropConfigProp = serializedObject.FindProperty("dropConfig");
            requiredTierProp = serializedObject.FindProperty("requiredTier");
            maxHealthProp = serializedObject.FindProperty("maxHealth");

            // Tree Model Slots
            fullTreeSlotProp = serializedObject.FindProperty("fullTreeSlot");
            upperPartSlotProp = serializedObject.FindProperty("upperPartSlot");
            stumpSlotProp = serializedObject.FindProperty("stumpSlot");

            // Hit Effects
            hitVFXProp = serializedObject.FindProperty("hitVFX");
            hitSoundProp = serializedObject.FindProperty("hitSound");
            shakeIntensityProp = serializedObject.FindProperty("shakeIntensity");
            shakeDurationProp = serializedObject.FindProperty("shakeDuration");

            // Fall Settings
            fallPushForceProp = serializedObject.FindProperty("fallPushForce");
            forcePointOffsetProp = serializedObject.FindProperty("forcePointOffset");
            fallDirectionProp = serializedObject.FindProperty("fallDirection");
            fallSoundProp = serializedObject.FindProperty("fallSound");
            fallImpactVFXProp = serializedObject.FindProperty("fallImpactVFX");

            // Gizmo Settings
            showForceGizmoProp = serializedObject.FindProperty("showForceGizmo");
            gizmoSphereRadiusProp = serializedObject.FindProperty("gizmoSphereRadius");
            gizmoArrowLengthProp = serializedObject.FindProperty("gizmoArrowLength");

            // Ground Detection
            impactDecelerationThresholdProp = serializedObject.FindProperty("impactDecelerationThreshold");

            // Drop Spawn Points
            dropSpawnPointsProp = serializedObject.FindProperty("dropSpawnPoints");

            // After Fall
            destroyDelayProp = serializedObject.FindProperty("destroyDelay");
            stumpLifetimeProp = serializedObject.FindProperty("stumpLifetime");


            RefreshDatabaseCache();
        }

        private void RefreshDatabaseCache()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

                if (cachedDatabase != null && cachedDatabase.Items != null)
                {
                    var items = cachedDatabase.Items.Where(i => i != null).ToList();
                    itemDataArray = items.ToArray();

                    var names = new List<string> { "(None)" };
                    foreach (var item in items)
                    {
                        string displayName = !string.IsNullOrEmpty(item.itemName)
                            ? $"{item.itemName} (ID: {item.itemId})"
                            : $"Unnamed (ID: {item.itemId})";
                        names.Add(displayName);
                    }
                    itemNames = names.ToArray();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Resource ID (from base class)
            var resourceIdProp = serializedObject.FindProperty("resourceId");
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(resourceIdProp, new GUIContent("Resource ID (Auto)"));
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Regenerate ID", GUILayout.Width(100)))
            {
                resourceIdProp.intValue = 0;
                serializedObject.ApplyModifiedProperties();
                // Trigger re-selection to regenerate
                Selection.activeObject = null;
                EditorApplication.delayCall += () => Selection.activeObject = target;
            }

            EditorGUILayout.Space(10);

            // Setup helper
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tree Setup Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Automatically configure model slots, add rigidbodies, search VFX, and setup drop points.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Configure Tree", GUILayout.Height(30)))
            {
                AutoConfigureTree((TreeResource)target);
            }

            if (GUILayout.Button("Create 3 Drop Spawn Points", GUILayout.Height(30)))
            {
                CreateDefaultSpawnPoints((TreeResource)target);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Resource Settings
            EditorGUILayout.LabelField("Resource Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(requiredTierProp);
            EditorGUILayout.PropertyField(maxHealthProp);

            EditorGUILayout.Space(10);

            // Tree Model Slots (3-slot system)
            EditorGUILayout.LabelField("Tree Model Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Setup: Full Tree (visible when alive), Upper Part (falls when cut, needs Rigidbody), Stump (shown when cut)", MessageType.Info);
            EditorGUILayout.PropertyField(fullTreeSlotProp, new GUIContent("Full Tree", "Complete tree model shown when alive"));
            EditorGUILayout.PropertyField(upperPartSlotProp, new GUIContent("Upper Part", "Trunk that falls - must have Rigidbody attached"));
            EditorGUILayout.PropertyField(stumpSlotProp, new GUIContent("Stump", "Stump that remains after cutting"));

            // Validation warning
            TreeResource treeResource = (TreeResource)target;
            var upperPartObj = upperPartSlotProp.objectReferenceValue as GameObject;
            if (upperPartObj != null && upperPartObj.GetComponent<Rigidbody>() == null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("Upper Part slot needs a Rigidbody component for falling physics!", MessageType.Warning);
                if (GUILayout.Button("Fix: Add Rigidbody", GUILayout.Width(130), GUILayout.Height(38)))
                {
                    Undo.RecordObject(upperPartObj, "Add Rigidbody");
                    var rb = upperPartObj.AddComponent<Rigidbody>();
                    rb.mass = 15f;
                    rb.linearDamping = 0.1f;
                    rb.angularDamping = 0.05f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.isKinematic = true;
                    EditorUtility.SetDirty(upperPartObj);
                    Debug.Log($"[TreeResourceEditor] Added and configured Rigidbody on '{upperPartObj.name}'");
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Drops
            EditorGUILayout.LabelField("Drops", EditorStyles.boldLabel);
            DrawDropConfig();

            EditorGUILayout.Space(10);

            // Hit Effects
            EditorGUILayout.LabelField("Hit Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitVFXProp, new GUIContent("Hit VFX", "ParticleSystem on tree - moves to hit point and plays"));
            EditorGUILayout.PropertyField(hitSoundProp);
            EditorGUILayout.PropertyField(shakeIntensityProp);
            EditorGUILayout.PropertyField(shakeDurationProp);

            EditorGUILayout.Space(10);

            // Fall Settings
            EditorGUILayout.LabelField("Fall Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fallPushForceProp, new GUIContent("Push Force", "Force applied at the force point to make tree fall"));
            EditorGUILayout.PropertyField(forcePointOffsetProp, new GUIContent("Force Point Offset", "Local offset from upper part where force is applied (higher = more realistic tip)"));
            EditorGUILayout.PropertyField(fallDirectionProp, new GUIContent("Fall Direction", "Local direction tree falls. Set to (0,0,0) to use incoming hit direction."));
            EditorGUILayout.PropertyField(fallSoundProp);
            EditorGUILayout.PropertyField(fallImpactVFXProp);

            EditorGUILayout.Space(5);

            // Gizmo Settings (foldout style)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Gizmo Preview", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(showForceGizmoProp, new GUIContent("Show Gizmo", "Show force point and direction in Scene view"));
            if (showForceGizmoProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(gizmoSphereRadiusProp, new GUIContent("Sphere Radius"));
                EditorGUILayout.PropertyField(gizmoArrowLengthProp, new GUIContent("Arrow Length"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Ground Detection
            EditorGUILayout.LabelField("Ground Detection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("1st ground hit = play impact VFX. 2nd ground hit (after bounce) = settled, spawn drops.", MessageType.Info);
            EditorGUILayout.PropertyField(impactDecelerationThresholdProp, new GUIContent("Impact Deceleration", "Deceleration spike (m/s²) that counts as a ground hit"));

            EditorGUILayout.Space(10);

            // Drop Spawn Points
            EditorGUILayout.LabelField("Drop Spawn Points", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Place empty GameObjects as children of Upper Part along the trunk. Drops will spawn at these points after the tree falls, aligned with the fallen trunk. If empty, drops spawn at the upper part center.", MessageType.Info);
            EditorGUILayout.PropertyField(dropSpawnPointsProp, new GUIContent("Spawn Points"), true);

            // Validation: warn if spawn points aren't children of upper part
            if (dropSpawnPointsProp.arraySize > 0 && upperPartSlotProp.objectReferenceValue != null)
            {
                var upperPart = upperPartSlotProp.objectReferenceValue as GameObject;
                bool hasNonChild = false;
                for (int i = 0; i < dropSpawnPointsProp.arraySize; i++)
                {
                    var spawnPoint = dropSpawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (spawnPoint != null && !spawnPoint.IsChildOf(upperPart.transform))
                    {
                        hasNonChild = true;
                        break;
                    }
                }
                if (hasNonChild)
                {
                    EditorGUILayout.HelpBox("Some spawn points are NOT children of the Upper Part! They must be children so they move with the falling tree.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            // After Fall
            EditorGUILayout.LabelField("After Fall", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(destroyDelayProp, new GUIContent("Destroy Delay", "Delay after settling before spawning wood"));
            EditorGUILayout.PropertyField(stumpLifetimeProp, new GUIContent("Stump Lifetime", "How long stump remains before being removed"));

            // Simulation tools (Play Mode only)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                GUIStyle boldDebugLabel = new GUIStyle(EditorStyles.boldLabel);
                boldDebugLabel.normal.textColor = new Color(0.9f, 0.5f, 0f);
                
                EditorGUILayout.LabelField("Simulation & Debugging Tools (Play Mode)", boldDebugLabel);
                EditorGUILayout.HelpBox("Test hits, falls, and respawn logic directly in the editor.", MessageType.None);
                
                EditorGUILayout.BeginHorizontal();
                
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("Simulate Hit", GUILayout.Height(28)))
                {
                    TreeResource tree = (TreeResource)target;
                    float currentHp = tree.HealthPercentage;
                    float nextHp = Mathf.Max(0f, currentHp - 0.2f);
                    Vector3 fakeHitPoint = tree.transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 0.2f;
                    
                    Debug.Log($"[TreeResourceEditor] Simulating hit on {tree.name}. Health: {currentHp * 100f:F0}% -> {nextHp * 100f:F0}%");
                    
                    tree.OnHit(fakeHitPoint, nextHp);
                    if (nextHp <= 0f)
                    {
                        tree.OnDestroyed(Vector3.forward);
                    }
                }
                
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("Simulate Fall", GUILayout.Height(28)))
                {
                    TreeResource tree = (TreeResource)target;
                    Debug.Log($"[TreeResourceEditor] Simulating complete fall on {tree.name}.");
                    tree.OnDestroyed(Vector3.forward);
                }
                
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Reset Tree", GUILayout.Height(28)))
                {
                    TreeResource tree = (TreeResource)target;
                    Debug.Log($"[TreeResourceEditor] Resetting tree {tree.name}.");
                    tree.OnReset();
                }
                
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoConfigureTree(TreeResource tree)
        {
            Undo.RecordObject(tree, "Auto-Configure Tree");

            // 1. Identify Slots
            Transform[] allChildren = tree.GetComponentsInChildren<Transform>(true);
            
            GameObject full = null;
            GameObject upper = null;
            GameObject stump = null;

            foreach (var child in allChildren)
            {
                if (child == tree.transform) continue;

                string nameLower = child.name.ToLower();

                if (full == null && (nameLower.Contains("full") || nameLower.Contains("alive") || nameLower.Contains("whole") || (nameLower.Contains("model") && !nameLower.Contains("stump") && !nameLower.Contains("upper") && !nameLower.Contains("trunk"))))
                {
                    full = child.gameObject;
                }
                
                if (stump == null && (nameLower.Contains("stump") || nameLower.Contains("base") || nameLower.Contains("cut")))
                {
                    stump = child.gameObject;
                }

                if (upper == null && (nameLower.Contains("upper") || nameLower.Contains("trunk") || nameLower.Contains("top") || nameLower.Contains("fall") || nameLower.Contains("log")))
                {
                    upper = child.gameObject;
                }
            }

            serializedObject.Update();
            
            if (full != null) serializedObject.FindProperty("fullTreeSlot").objectReferenceValue = full;
            if (upper != null) serializedObject.FindProperty("upperPartSlot").objectReferenceValue = upper;
            if (stump != null) serializedObject.FindProperty("stumpSlot").objectReferenceValue = stump;

            // 2. Configure Rigidbody on Upper Part
            if (upper != null)
            {
                var rb = upper.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = upper.AddComponent<Rigidbody>();
                    rb.mass = 15f;
                    rb.linearDamping = 0.1f;
                    rb.angularDamping = 0.05f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }
                rb.isKinematic = true;
            }

            // 3. Locate and assign VFX from children
            ParticleSystem hitPS = null;
            ParticleSystem fallPS = null;
            
            var particleSystems = tree.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                string psName = ps.name.ToLower();
                if (psName.Contains("hit") || psName.Contains("chop") || psName.Contains("cut") || psName.Contains("dmg") || psName.Contains("damage"))
                {
                    hitPS = ps;
                }
                else if (psName.Contains("fall") || psName.Contains("impact") || psName.Contains("land") || psName.Contains("dust"))
                {
                    fallPS = ps;
                }
            }

            if (particleSystems.Length == 1)
            {
                hitPS = particleSystems[0];
            }
            else if (particleSystems.Length >= 2 && hitPS == null && fallPS == null)
            {
                hitPS = particleSystems[0];
                fallPS = particleSystems[1];
            }

            if (hitPS != null) serializedObject.FindProperty("hitVFX").objectReferenceValue = hitPS;
            if (fallPS != null) serializedObject.FindProperty("fallImpactVFX").objectReferenceValue = fallPS;

            // 4. Auto-populate drop spawn points from children of Upper Part if currently empty
            var dropPointsProp = serializedObject.FindProperty("dropSpawnPoints");
            if (upper != null && dropPointsProp.arraySize == 0)
            {
                var spawnPointsList = new List<Transform>();
                foreach (var child in upper.GetComponentsInChildren<Transform>(true))
                {
                    if (child == upper.transform) continue;
                    string childName = child.name.ToLower();
                    if (childName.Contains("spawn") || childName.Contains("drop") || childName.Contains("point") || childName.Contains("wood"))
                    {
                        spawnPointsList.Add(child);
                    }
                }

                if (spawnPointsList.Count > 0)
                {
                    dropPointsProp.ClearArray();
                    for (int i = 0; i < spawnPointsList.Count; i++)
                    {
                        dropPointsProp.InsertArrayElementAtIndex(i);
                        dropPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPointsList[i];
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            
            Debug.Log($"[TreeResourceEditor] Auto-configured '{tree.gameObject.name}'. Check console logs for details.");
        }

        private void CreateDefaultSpawnPoints(TreeResource tree)
        {
            serializedObject.Update();
            var upperPartProp = serializedObject.FindProperty("upperPartSlot");
            var upper = upperPartProp.objectReferenceValue as GameObject;

            if (upper == null)
            {
                EditorUtility.DisplayDialog("Error", "Upper Part Slot must be assigned first!", "OK");
                return;
            }

            Undo.RecordObject(tree, "Create Default Spawn Points");

            var dropPointsProp = serializedObject.FindProperty("dropSpawnPoints");
            dropPointsProp.ClearArray();

            float heightOffset = 1.5f;
            for (int i = 0; i < 3; i++)
            {
                GameObject spawnGo = new GameObject($"DropSpawnPoint_{i + 1}");
                Undo.RegisterCreatedObjectUndo(spawnGo, "Create Drop Spawn Point");
                
                spawnGo.transform.SetParent(upper.transform);
                spawnGo.transform.localPosition = new Vector3(0f, heightOffset * (i + 1), 0f);
                spawnGo.transform.localRotation = Quaternion.identity;

                dropPointsProp.InsertArrayElementAtIndex(i);
                dropPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnGo.transform;
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[TreeResourceEditor] Created 3 default drop spawn points under '{upper.name}'.");
        }

        private void DrawDropConfig()
        {
            var dropItemProp = dropConfigProp.FindPropertyRelative("dropItem");
            var minDropsProp = dropConfigProp.FindPropertyRelative("minDrops");
            var maxDropsProp = dropConfigProp.FindPropertyRelative("maxDrops");

            // Item dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Drop Item", GUILayout.Width(EditorGUIUtility.labelWidth - 4));

            if (cachedDatabase == null || itemDataArray == null || itemDataArray.Length == 0)
            {
                EditorGUILayout.LabelField("No ItemDatabase found!", EditorStyles.helpBox);
                if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                {
                    RefreshDatabaseCache();
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            // Find current selection
            int currentIndex = 0;
            var currentItem = dropItemProp.objectReferenceValue as InventoryItemData;
            if (currentItem != null)
            {
                for (int i = 0; i < itemDataArray.Length; i++)
                {
                    if (itemDataArray[i] == currentItem)
                    {
                        currentIndex = i + 1;
                        break;
                    }
                }
            }

            // Draw dropdown
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(currentIndex, itemNames);

            if (GUILayout.Button("↻", GUILayout.Width(25)))
            {
                RefreshDatabaseCache();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                dropItemProp.objectReferenceValue = newIndex == 0 ? null : itemDataArray[newIndex - 1];
            }

            // Show item preview
            if (currentItem != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                if (currentItem.itemIcon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                    DrawSpriteIcon(iconRect, currentItem.itemIcon);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(currentItem.itemName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Type: {currentItem.objectType}");
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            // Min/Max drops
            EditorGUILayout.PropertyField(minDropsProp);
            EditorGUILayout.PropertyField(maxDropsProp);
        }

        private void DrawSpriteIcon(Rect position, Sprite sprite)
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

            GUI.DrawTextureWithTexCoords(position, tex, texCoords);
        }
    }
}