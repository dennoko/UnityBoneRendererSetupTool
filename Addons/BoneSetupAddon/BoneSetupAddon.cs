using Hays.BoneRendererSetup.Core;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// Bone Setup Tools Addon
    /// L/R Sync と Uniform Scale 機能を提供する
    /// </summary>
    public class BoneSetupAddon : IAddonFeature
    {
        public string DisplayName => "ボーンセットアップツール";

        private readonly LRSyncFeature _lrSync = new LRSyncFeature();
        private readonly UniformScaleFeature _uniformScale = new UniformScaleFeature();

        // ツールチップ付きラベル
        private static readonly GUIContent LabelLRSync = new GUIContent(
            "左右ボーン同期", 
            "選択したボーンの変更（位置・回転・スケール）を対になるボーン（左右）に自動で反映します。");
        
        private static readonly GUIContent ToggleLRSync = new GUIContent(
            "同期を有効化", 
            "有効にすると、ボーンの編集が反対側のボーンにミラーリングされます。\nMA Scale Adjuster の値も同期されます。");
        
        private static readonly GUIContent LabelUniformScale = new GUIContent(
            "均一スケール", 
            "ボーンのスケールを X/Y/Z すべて同じ値に保つ機能です。\n子ボーンの回転時にスケールが歪む問題を防ぎます。");
        
        private static readonly GUIContent ToggleUniformScale = new GUIContent(
            "均一スケールを有効化", 
            "有効にすると、Scale の X/Y/Z いずれかを変更した際に、他の軸も自動で同じ値に揃います。");

        public void OnEnable()
        {
            _lrSync.Subscribe();
            _uniformScale.Subscribe();
        }

        public void OnDisable()
        {
            _lrSync.Unsubscribe();
            _uniformScale.Unsubscribe();
        }

        public void OnGUI(GameObject avatar, GameObject outfit)
        {
            // L/R Sync UI
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(LabelLRSync, EditorStyles.boldLabel);
            
            _lrSync.Enabled = EditorGUILayout.Toggle(ToggleLRSync, _lrSync.Enabled);
            
            if (_lrSync.Enabled)
            {
               if (_lrSync.CachedRoot != null)
               {
                   EditorGUILayout.HelpBox($"対象 Armature: {_lrSync.CachedRoot.name}\nキャッシュ済みペア数: {_lrSync.CachedPairCount}", MessageType.Info);
               }
               else
               {
                   EditorGUILayout.HelpBox("ボーンを選択すると同期が有効になります。", MessageType.None);
               }
            }
            EditorGUILayout.EndVertical();

            // Uniform Scale UI
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(LabelUniformScale, EditorStyles.boldLabel);
            
            _uniformScale.Enabled = EditorGUILayout.Toggle(ToggleUniformScale, _uniformScale.Enabled);
            
            if (_uniformScale.Enabled)
            {
                EditorGUILayout.HelpBox("有効時、Scale の X/Y/Z いずれかを変更すると、他の軸も同じ値に揃います。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            // SceneGUI processing is handled by EditorApplication.update
        }
    }
}
