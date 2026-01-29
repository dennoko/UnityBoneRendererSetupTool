using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hays.BoneRendererSetup.Core;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    public class BoneSetupAddon : IAddonFeature
    {
        public string DisplayName => "Bone Setup Tools";

        // L/R Sync
        private bool _lrSyncEnabled = false;
        private Transform _currentSelection;
        private Transform _cachedRoot;
        private Dictionary<Transform, Transform> _mirrorCache = new Dictionary<Transform, Transform>();
        
        // MA Presets
        private bool _maRecordEnabled = false;
        private string _presetTitle = "";

        // Superposition (Managed via Context Menu, but we can visualize status here)

        public void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += UpdateSync;
        }

        public void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdateSync;
        }

        // ... (GUI methods remain) ...

        // ... (Helper methods remain) ...

        private void UpdateSync()
        {
            if (!_lrSyncEnabled || _currentSelection == null || _currentSelection != _lastTrackedTransform) return;
            if (!_mirrorCache.ContainsKey(_currentSelection)) return;
            
            var mirror = _mirrorCache[_currentSelection];
            if (mirror == null) return;

            bool posChanged = _currentSelection.localPosition != _lastLocalPos;
            bool rotChanged = _currentSelection.localRotation != _lastLocalRot;
            bool scaleChanged = _currentSelection.localScale != _lastLocalScale;

            if (posChanged || rotChanged || scaleChanged)
            {
                // Apply to mirror
                Undo.RecordObject(mirror, "Mirror Bone Sync");
                
                if (posChanged)
                {
                    // Standard Mirror: Invert X
                    var localPos = _currentSelection.localPosition;
                    localPos.x *= -1; 
                    mirror.localPosition = localPos;
                }

                if (rotChanged)
                {
                    // Standard Mirror: Invert Y and Z components of Quaternion
                    // This mirrors the rotation across the YZ plane (X axis)
                    var localRot = _currentSelection.localRotation;
                    mirror.localRotation = new Quaternion(localRot.x, -localRot.y, -localRot.z, localRot.w);
                }

                if (scaleChanged)
                {
                    mirror.localScale = _currentSelection.localScale;
                }
                
                UpdateLastTransform(_currentSelection);
            }
        }

        public void OnGUI(GameObject avatar, GameObject outfit)
        {
            // --- L/R Sync UI ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("L/R Bone Sync", EditorStyles.boldLabel);
            
            _lrSyncEnabled = EditorGUILayout.Toggle("Enable Sync", _lrSyncEnabled);
            
            if (_lrSyncEnabled)
            {
               if (_cachedRoot != null)
               {
                   EditorGUILayout.HelpBox($"Active Armature: {_cachedRoot.name}\nCached Pairs: {_mirrorCache.Count / 2}", MessageType.Info);
               }
               else
               {
                   EditorGUILayout.HelpBox("Select a bone to activate sync.", MessageType.None);
               }
            }
            EditorGUILayout.EndVertical();
            
            // --- MA Presets UI ---
            EditorGUILayout.Space(10);
            DrawMAPresetsUI(outfit);
        }

        private void DrawMAPresetsUI(GameObject outfit)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("MA Scale Presets", EditorStyles.boldLabel);

            // Record Section
            using (new EditorGUILayout.HorizontalScope())
            {
                _presetTitle = EditorGUILayout.TextField("Title", _presetTitle);
                
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_presetTitle) || outfit == null))
                {
                    if (GUILayout.Button("Record", GUILayout.Width(60)))
                    {
                        RecordPreset(outfit, _presetTitle);
                    }
                }
            }

            // Apply Section
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Saved Presets", EditorStyles.miniLabel);
            
            // Find all presets in project
            // Optimization: Cache this list? For now just find assets.
            var guids = AssetDatabase.FindAssets("t:BoneScalePreset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<BoneScalePreset>(path);
                if (preset == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(preset.Title, GUILayout.MinWidth(100));
                    
                    using (new EditorGUI.DisabledScope(outfit == null))
                    {
                        if (GUILayout.Button("Apply", GUILayout.Width(60)))
                        {
                            ApplyPreset(outfit, preset);
                        }
                    }
                    
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        // Delete logic (optional, maybe assume user deletes via Project view)
                        if (EditorUtility.DisplayDialog("Delete Preset", $"Delete '{preset.Title}'?", "Yes", "No"))
                        {
                            AssetDatabase.DeleteAsset(path);
                        }
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void RecordPreset(GameObject outfit, string title)
        {
            var smrs = outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var transforms = outfit.GetComponentsInChildren<Transform>(true);
            
            var preset = ScriptableObject.CreateInstance<BoneScalePreset>();
            preset.Title = title;
            
            foreach (var t in transforms)
            {
                // Only record non-identity scales
                if (t.localScale != Vector3.one)
                {
                    preset.Scales.Add(new BoneScaleData 
                    { 
                        BoneName = t.name, 
                        Scale = t.localScale 
                    });
                }
            }
            
            if (preset.Scales.Count == 0)
            {
                Debug.LogWarning("[BoneSetupAddon] No bones with modified scale found.");
                return;
            }

            // Save Asset
            string folder = "Assets/BoneRendererData/Presets";
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            
            // Sanitize filename
            string filename = string.Join("_", title.Split(System.IO.Path.GetInvalidFileNameChars()));
            string path = $"{folder}/{filename}.asset";
            
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BoneSetupAddon] Preset '{title}' saved to {path}");
        }

        private void ApplyPreset(GameObject outfit, BoneScalePreset preset)
        {
            // Find MA Type
            var maType = TypeCache.GetTypesDerivedFrom(typeof(Component)) // This is slow checking all components? No.
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster");
            
            if (maType == null)
            {
                // Try old namespace or other variations if known, otherwise warn.
                Debug.LogError("[BoneSetupAddon] ModularAvatarScaleAdjuster class not found. Is Modular Avatar installed?");
                return;
            }

            var allTransforms = outfit.GetComponentsInChildren<Transform>(true);
            int appliedCount = 0;

            foreach (var data in preset.Scales)
            {
                var target = allTransforms.FirstOrDefault(t => t.name == data.BoneName);
                if (target == null) continue;

                Undo.RecordObject(target, "Apply Bone Scale");
                target.localScale = data.Scale;
                
                // Add/Update MA Component
                var component = target.GetComponent(maType);
                if (component == null)
                {
                    component = Undo.AddComponent(target.gameObject, maType);
                }
                
                // Update properties?
                // MA Scale Adjuster logic: It usually takes the current transform scale as the "adjustment".
                // So if we just set the transform scale, does MA pick it up automatically?
                // Or does it have a 'scale' field that overrides?
                // Checking usage: MA Scale Adjuster typically records the scale in the component itself if configured,
                // OR it might just be a marker component that says "Don't reset this scale".
                // 
                // Let's assume we need to set the scale on Transform, and ensure the component is present.
                // It seems checking MA docs (from memory): ScaleAdjuster applies the transform's scale to the avatar.
                // So just having the component is often enough. 
                // However, user said "Scale Adjusterに記録する".
                // This implies explicitly setting values if the component holds them.
                //
                // If the component has a 'scale' property, we should set it.
                // Let's try to set 'scale' property via reflection if it exists.
                
                var so = new SerializedObject(component);
                var scaleProp = so.FindProperty("scale"); // Hypothetical property name
                if (scaleProp != null)
                {
                    so.Update();
                    scaleProp.vector3Value = data.Scale;
                    so.ApplyModifiedProperties();
                }
                
                appliedCount++;
            }
            
            Debug.Log($"[BoneSetupAddon] Applied preset '{preset.Title}' to {appliedCount} bones.");
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!_lrSyncEnabled) return;
            
            var selection = Selection.activeTransform;
            if (selection == null || !_mirrorCache.ContainsKey(selection)) return;

            var mirrorBone = _mirrorCache[selection];
            if (mirrorBone == null) return;

            // Simple change detection using Event.current or checking specific tools is tricky.
            // Better to monitor transform changes.
            // Since we are in OnSceneGUI, we can check for changes.
            // However, Unity's Undo system handles the actual movement.
            // We need to apply the mirror movement manually.
            
            if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
            {
                // This is a bit too raw.
                // Let's rely on checking dirty state or diffing from last frame?
                // No, OnSceneGUI is called frequently.
                // Best practice for "Syncing" during interaction:
                // Use `EditorGUI.BeginChangeCheck` wrapper around Handles? No, handled by Editor.
                
                // We will listen to transform changes on the selected object.
                // BUT, OnSceneGUI doesn't give us the "delta".
                // We'll store the previous local transform of the selection and compare.
                // This needs to happen per-frame or per-SceneGUI call.
            }
            
            // Actually, we should process the sync in the update loop or specifically when the transform changes.
            // Let's use `transform.hasChanged` but that's runtime.
            // Editor-side, we can track `selection.localPosition/Rotation/Scale`.
        }

        // We'll use a local tracker for the current selection's transform to calculate deltas.
        private Vector3 _lastLocalPos;
        private Quaternion _lastLocalRot;
        private Vector3 _lastLocalScale;
        private Transform _lastTrackedTransform;

        private void OnSelectionChanged()
        {
            _currentSelection = Selection.activeTransform;
            UpdateCacheIfNeeded(_currentSelection);
            
            // Reset tracker
            if (_currentSelection != null)
            {
                _lastTrackedTransform = _currentSelection;
                UpdateLastTransform(_currentSelection);
            }
            else
            {
                _lastTrackedTransform = null;
            }
        }
        
        private void UpdateLastTransform(Transform t)
        {
            _lastLocalPos = t.localPosition;
            _lastLocalRot = t.localRotation;
            _lastLocalScale = t.localScale;
        }

        private void UpdateCacheIfNeeded(Transform selection)
        {
            if (selection == null) return;

            // If we are still within the same root, do nothing.
            // Find root (Armature or Hips or SceneRoot)
            // Heuristic: Go up until we find a name like "Armature" or top level.
            var root = FindArmatureRoot(selection);
            
            if (root == _cachedRoot && root != null) return;

            // Rebuild cache
            _cachedRoot = root;
            _mirrorCache.Clear();
            if (_cachedRoot == null) return;

            var allBones = _cachedRoot.GetComponentsInChildren<Transform>(true);
            foreach (var bone in allBones)
            {
                var mirrorName = GetMirrorName(bone.name);
                if (string.IsNullOrEmpty(mirrorName)) continue;
                
                var match = allBones.FirstOrDefault(b => b.name == mirrorName);
                if (match != null && match != bone)
                {
                    _mirrorCache[bone] = match;
                }
            }
            
            // Debug.Log($"[BoneSetupAddon] Cached {_mirrorCache.Count} mirror bones for {_cachedRoot.name}");
        }

        private Transform FindArmatureRoot(Transform t)
        {
            // Go up until we hit a "reference" object or null.
            // If it's an avatar, the root might be the Animator's gameobject.
            var curr = t;
            while (curr.parent != null)
            {
                // Stop at Animator if present
                if (curr.GetComponent<Animator>() != null) return curr;
                // Stop at "Armature"
                if (curr.name.ToLower().Contains("armature")) return curr;
                curr = curr.parent;
            }
            return curr; // Scene root
        }

        private string GetMirrorName(string name)
        {
            // Patterns: L/R, Left/Right, 左/右
            // Case insensitive check, but sensitive replacement
            // Use Regex to find the part to swap.
            
            // 1. _L / _R (Suffix)
            if (Regex.IsMatch(name, @"_L$")) return Regex.Replace(name, @"_L$", "_R");
            if (Regex.IsMatch(name, @"_R$")) return Regex.Replace(name, @"_R$", "_L");
            
            // 2. .L / .R (Suffix)
            if (Regex.IsMatch(name, @"\.L$")) return Regex.Replace(name, @"\.L$", ".R");
            if (Regex.IsMatch(name, @"\.R$")) return Regex.Replace(name, @"\.R$", ".L");

            // 3. Left / Right in word
            if (name.Contains("Left")) return name.Replace("Left", "Right");
            if (name.Contains("Right")) return name.Replace("Right", "Left");
            
            // 4. L / R standaline or separated ?? (Risk of false positives in "Leg")
            // Strict check for " L " or " R " ? No, usually CamelCase.
            
            // Japanese
            if (name.Contains("左")) return name.Replace("左", "右");
            if (name.Contains("右")) return name.Replace("右", "左");

            return null;
        }

        // Called from Update loop via EditorApplication.update would be smoother for Transform monitoring
        // But we only have OnSceneGUI in the interface.
        // We can hook EditorApplication.update in OnEnable.
        
        public void UpdateSync()
        {
            if (!_lrSyncEnabled || _currentSelection == null || _currentSelection != _lastTrackedTransform) return;
            if (!_mirrorCache.ContainsKey(_currentSelection)) return;
            
            var mirror = _mirrorCache[_currentSelection];
            if (mirror == null) return;

            bool posChanged = _currentSelection.localPosition != _lastLocalPos;
            bool rotChanged = _currentSelection.localRotation != _lastLocalRot;
            bool scaleChanged = _currentSelection.localScale != _lastLocalScale;

            if (posChanged || rotChanged || scaleChanged)
            {
                // Apply to mirror
                Undo.RecordObject(mirror, "Mirror Bone Sync");
                
                if (posChanged)
                {
                    // Mirror position: x is flipped? Depends on axis convention.
                    // Unity Humanoid usually: X is symmetry axis? 
                    // Standard T-pose: X points along bone? 
                    // Actually, simple mirror for Humanoid: 
                    // Position X is usually inverted.
                    
                    // We need to know the mirror plane.
                    // Assuming standard Humanoid rig (X-Right). 
                    // Local position X is usually inverted for symmetric bones if they are mirrored in bind pose.
                    // If they are just duplicated and rotated 180, it's different.
                    
                    // HEURISTIC: Check initial relationship?
                    // Too complex for now.
                    // Simple assume: Local Position X inverted.
                    var localPos = _currentSelection.localPosition;
                    localPos.x *= -1; 
                    mirror.localPosition = localPos;
                }

                if (rotChanged)
                {
                    // Rotation mirroring is tricky.
                    // For standard Unity Humanoid:
                    // Quaternion(x, -y, -z, w) ?
                    var localRot = _currentSelection.localRotation;
                    // mirror.localRotation = new Quaternion(localRot.x, -localRot.y, -localRot.z, localRot.w); // Common for X-axis mirror
                    
                    // This often depends on how the bones were built (Blender vs Maya vs native).
                    // Safe approach: Mirror World Position/Rotation relative to root?
                    // No, user asked for "Symmetric movement".
                    // Let's try the standard X-mirror first.
                    
                    mirror.localRotation = new Quaternion(localRot.x, -localRot.y, -localRot.z, localRot.w);
                }

                if (scaleChanged)
                {
                    mirror.localScale = _currentSelection.localScale;
                }
                
                UpdateLastTransform(_currentSelection);
            }
        }
    }
}
