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
        public string DisplayName => "Bone Setup Tools";

        private readonly LRSyncFeature _lrSync = new LRSyncFeature();
        private readonly UniformScaleFeature _uniformScale = new UniformScaleFeature();

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
            EditorGUILayout.LabelField("L/R Bone Sync", EditorStyles.boldLabel);
            
            _lrSync.Enabled = EditorGUILayout.Toggle("Enable Sync", _lrSync.Enabled);
            
            if (_lrSync.Enabled)
            {
               if (_lrSync.CachedRoot != null)
               {
                   EditorGUILayout.HelpBox($"Active Armature: {_lrSync.CachedRoot.name}\nCached Pairs: {_lrSync.CachedPairCount}", MessageType.Info);
               }
               else
               {
                   EditorGUILayout.HelpBox("Select a bone to activate sync.", MessageType.None);
               }
            }
            EditorGUILayout.EndVertical();

            // Uniform Scale UI
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Uniform Scale", EditorStyles.boldLabel);
            
            _uniformScale.Enabled = EditorGUILayout.Toggle("Enable Uniform Scale", _uniformScale.Enabled);
            
            if (_uniformScale.Enabled)
            {
                EditorGUILayout.HelpBox("When enabled, changing any scale axis (X/Y/Z) will set all axes to the same value.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            // SceneGUI processing is handled by EditorApplication.update
        }
    }
}
