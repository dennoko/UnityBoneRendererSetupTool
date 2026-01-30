using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// Uniform Scale 機能
    /// 選択中のボーンのスケールX/Y/Zのいずれかが変更されたとき、他の2つも同じ値に揃える
    /// 子ボーンが回転した際のスケール歪み問題を回避するため
    /// </summary>
    public class UniformScaleFeature
    {
        private bool _enabled = true;
        private Transform _currentSelection;
        private Vector3 _lastLocalScale;
        private Transform _lastTrackedTransform;

        // MA Scale Adjuster Type (cached)
        private System.Type _maScaleAdjusterType;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public void Subscribe()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += UpdateUniformScale;
            CacheMAType();
        }

        public void Unsubscribe()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdateUniformScale;
        }

        private void CacheMAType()
        {
            _maScaleAdjusterType = TypeCache.GetTypesDerivedFrom(typeof(Component))
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster");
        }

        private void OnSelectionChanged()
        {
            _currentSelection = Selection.activeTransform;
            
            if (_currentSelection != null)
            {
                _lastTrackedTransform = _currentSelection;
                _lastLocalScale = _currentSelection.localScale;
            }
            else
            {
                _lastTrackedTransform = null;
            }
        }

        private void UpdateUniformScale()
        {
            if (!_enabled || _currentSelection == null || _currentSelection != _lastTrackedTransform) return;

            Vector3 currentScale = _currentSelection.localScale;
            
            // スケールが変わっていなければ何もしない
            if (currentScale == _lastLocalScale) return;

            // どの成分が変更されたかを検出
            bool xChanged = !Mathf.Approximately(currentScale.x, _lastLocalScale.x);
            bool yChanged = !Mathf.Approximately(currentScale.y, _lastLocalScale.y);
            bool zChanged = !Mathf.Approximately(currentScale.z, _lastLocalScale.z);

            float newUniformValue;

            // 変更された成分の値を使う（複数変更された場合は最初に見つかったものを優先）
            if (xChanged)
            {
                newUniformValue = currentScale.x;
            }
            else if (yChanged)
            {
                newUniformValue = currentScale.y;
            }
            else if (zChanged)
            {
                newUniformValue = currentScale.z;
            }
            else
            {
                // 変更なし（floatの誤差など）
                _lastLocalScale = currentScale;
                return;
            }

            Vector3 uniformScale = new Vector3(newUniformValue, newUniformValue, newUniformValue);

            // 既に均一ならスキップ
            if (currentScale == uniformScale)
            {
                _lastLocalScale = currentScale;
                return;
            }

            // Transformに適用
            Undo.RecordObject(_currentSelection, "Uniform Scale");
            _currentSelection.localScale = uniformScale;

            // MA Scale Adjuster にも適用
            if (_maScaleAdjusterType != null)
            {
                var maComponent = _currentSelection.GetComponent(_maScaleAdjusterType);
                if (maComponent != null)
                {
                    var so = new SerializedObject(maComponent);
                    var scaleProp = so.FindProperty("m_Scale");
                    if (scaleProp != null)
                    {
                        so.Update();
                        scaleProp.vector3Value = uniformScale;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            _lastLocalScale = uniformScale;
        }
    }
}
