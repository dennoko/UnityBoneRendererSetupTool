using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// L/R Bone Sync機能
    /// 選択中のボーンの移動・回転・スケール変更を対になるボーンにミラーリングする
    /// </summary>
    public class LRSyncFeature
    {
        private bool _enabled = false;
        private Transform _currentSelection;
        private Transform _cachedRoot;
        private Dictionary<Transform, Transform> _mirrorCache = new Dictionary<Transform, Transform>();
        
        private Vector3 _lastLocalPos;
        private Quaternion _lastLocalRot;
        private Vector3 _lastLocalScale;
        private Transform _lastTrackedTransform;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public Transform CachedRoot => _cachedRoot;
        public int CachedPairCount => _mirrorCache.Count / 2;

        public void Subscribe()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += UpdateSync;
        }

        public void Unsubscribe()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdateSync;
        }

        private void OnSelectionChanged()
        {
            _currentSelection = Selection.activeTransform;
            UpdateCacheIfNeeded(_currentSelection);
            
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

        private void UpdateSync()
        {
            if (!_enabled || _currentSelection == null || _currentSelection != _lastTrackedTransform) return;
            if (!_mirrorCache.ContainsKey(_currentSelection)) return;
            
            var mirror = _mirrorCache[_currentSelection];
            if (mirror == null) return;

            bool posChanged = _currentSelection.localPosition != _lastLocalPos;
            bool rotChanged = _currentSelection.localRotation != _lastLocalRot;
            bool scaleChanged = _currentSelection.localScale != _lastLocalScale;

            if (posChanged || rotChanged || scaleChanged)
            {
                Undo.RecordObject(mirror, "Mirror Bone Sync");
                
                if (posChanged)
                {
                    var localPos = _currentSelection.localPosition;
                    localPos.x *= -1; 
                    mirror.localPosition = localPos;
                }

                if (rotChanged)
                {
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

        private void UpdateCacheIfNeeded(Transform selection)
        {
            if (selection == null) return;

            var root = FindArmatureRoot(selection);
            
            if (root == _cachedRoot && root != null) return;

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
        }

        private Transform FindArmatureRoot(Transform t)
        {
            var curr = t;
            while (curr.parent != null)
            {
                if (curr.GetComponent<Animator>() != null) return curr;
                if (curr.name.ToLower().Contains("armature")) return curr;
                curr = curr.parent;
            }
            return curr;
        }

        private string GetMirrorName(string name)
        {
            // _L / _R
            if (Regex.IsMatch(name, @"_L$")) return Regex.Replace(name, @"_L$", "_R");
            if (Regex.IsMatch(name, @"_R$")) return Regex.Replace(name, @"_R$", "_L");
            
            // .L / .R
            if (Regex.IsMatch(name, @"\.L$")) return Regex.Replace(name, @"\.L$", ".R");
            if (Regex.IsMatch(name, @"\.R$")) return Regex.Replace(name, @"\.R$", ".L");

            // Left / Right
            if (name.Contains("Left")) return name.Replace("Left", "Right");
            if (name.Contains("Right")) return name.Replace("Right", "Left");
            
            // Japanese
            if (name.Contains("左")) return name.Replace("左", "右");
            if (name.Contains("右")) return name.Replace("右", "左");

            return null;
        }
    }
}
