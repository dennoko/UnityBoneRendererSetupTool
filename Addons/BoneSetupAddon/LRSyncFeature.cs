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
    /// MA Scale Adjuster コンポーネントも同期する
    /// </summary>
    public class LRSyncFeature
    {
        private bool _enabled = true;
        private Transform _currentSelection;
        private Transform _cachedRoot;
        private Dictionary<Transform, Transform> _mirrorCache = new Dictionary<Transform, Transform>();
        
        private Vector3 _lastLocalPos;
        private Quaternion _lastLocalRot;
        private Vector3 _lastLocalScale;
        private Transform _lastTrackedTransform;

        // MA Scale Adjuster Type (cached)
        private System.Type _maScaleAdjusterType;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public GameObject TargetAvatar { get; set; }

        public Transform CachedRoot => _cachedRoot;
        public int CachedPairCount => _mirrorCache.Count / 2;

        public void Subscribe()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += UpdateSync;
            CacheMAType();
        }

        public void Unsubscribe()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdateSync;
        }

        private void CacheMAType()
        {
            _maScaleAdjusterType = TypeCache.GetTypesDerivedFrom(typeof(Component))
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster");
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
            if (!_enabled || TargetAvatar == null || _currentSelection == null || _currentSelection != _lastTrackedTransform) return;
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

            // MA Scale Adjuster の同期
            SyncMAScaleAdjuster(_currentSelection, mirror);
        }

        /// <summary>
        /// MA Scale Adjuster コンポーネントを同期する
        /// 子オブジェクトの位置も調整する
        /// </summary>
        private void SyncMAScaleAdjuster(Transform source, Transform mirror)
        {
            if (_maScaleAdjusterType == null) return;

            var sourceComponent = source.GetComponent(_maScaleAdjusterType);
            var mirrorComponent = mirror.GetComponent(_maScaleAdjusterType);

            if (sourceComponent != null)
            {
                // ソースにコンポーネントがある場合、ミラーにも追加/同期
                if (mirrorComponent == null)
                {
                    mirrorComponent = Undo.AddComponent(mirror.gameObject, _maScaleAdjusterType);
                }

                // スケール値を同期
                var sourceSO = new SerializedObject(sourceComponent);
                var mirrorSO = new SerializedObject(mirrorComponent);

                var sourceScale = sourceSO.FindProperty("m_Scale");
                var mirrorScale = mirrorSO.FindProperty("m_Scale");

                if (sourceScale != null && mirrorScale != null)
                {
                    Vector3 newScale = sourceScale.vector3Value;
                    Vector3 oldScale = mirrorScale.vector3Value;
                    
                    if (oldScale != newScale)
                    {
                        // MAの設定で子オブジェクトの位置調整が有効な場合のみ調整
                        if (IsAdjustChildPositionsEnabled())
                        {
                            AdjustChildPositions(mirror, oldScale, newScale);
                        }
                        
                        // スケール値を更新
                        Undo.RecordObject(mirrorComponent, "Mirror MA Scale Adjuster");
                        mirrorSO.Update();
                        mirrorScale.vector3Value = newScale;
                        mirrorSO.ApplyModifiedProperties();
                    }
                }
            }
            else if (mirrorComponent != null)
            {
                // ソースにコンポーネントがなく、ミラーにある場合は削除
                Undo.DestroyObjectImmediate(mirrorComponent);
            }
        }

        /// <summary>
        /// スケール変更時に子オブジェクトの位置を調整する
        /// MAのScaleAdjusterToolと同じロジック
        /// </summary>
        private void AdjustChildPositions(Transform target, Vector3 oldScale, Vector3 newScale)
        {
            const float THRESHOLD = 1f / (1 << 14); // 1/16384
            
            // スケールをクランプ（ゼロ除算回避）
            oldScale = new Vector3(
                Mathf.Max(THRESHOLD, oldScale.x),
                Mathf.Max(THRESHOLD, oldScale.y),
                Mathf.Max(THRESHOLD, oldScale.z)
            );
            newScale = new Vector3(
                Mathf.Max(THRESHOLD, newScale.x),
                Mathf.Max(THRESHOLD, newScale.y),
                Mathf.Max(THRESHOLD, newScale.z)
            );

            var targetL2W = target.localToWorldMatrix;
            var baseToScaleCoord = (targetL2W * Matrix4x4.Scale(oldScale)).inverse * targetL2W;
            var scaleToBaseCoord = Matrix4x4.Scale(newScale);
            var updateTransform = scaleToBaseCoord * baseToScaleCoord;

            foreach (Transform child in target)
            {
                Undo.RecordObject(child, "Adjust Child Position");
                child.localPosition = updateTransform.MultiplyPoint(child.localPosition);
            }
        }

        /// <summary>
        /// MAのScaleAdjusterToolの「子オブジェクトの位置を調整」設定を取得
        /// </summary>
        private bool IsAdjustChildPositionsEnabled()
        {
            // nadena.dev.modular_avatar.core.editor.ScaleAdjusterTool.AdjustChildPositions を参照
            var scaleAdjusterToolType = TypeCache.GetTypesDerivedFrom(typeof(object))
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.editor.ScaleAdjusterTool");
            
            if (scaleAdjusterToolType == null) return false;

            var field = scaleAdjusterToolType.GetField("AdjustChildPositions", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (field == null) return false;

            return (bool)field.GetValue(null);
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

