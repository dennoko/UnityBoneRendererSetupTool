using Hays.BoneRendererSetup.Core;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// Bone Setup Tools Addon
    /// L/R Sync と MA Scale Presets 機能を提供する
    /// </summary>
    public class BoneSetupAddon : IAddonFeature
    {
        public string DisplayName => "Bone Setup Tools";

        private readonly LRSyncFeature _lrSync = new LRSyncFeature();
        private readonly MAPresetFeature _maPreset = new MAPresetFeature();

        public void OnEnable()
        {
            _lrSync.Subscribe();
        }

        public void OnDisable()
        {
            _lrSync.Unsubscribe();
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
            
            // MA Presets UI
            EditorGUILayout.Space(10);
            _maPreset.DrawUI(outfit);
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            // SceneGUI processing is handled by EditorApplication.update in LRSyncFeature
        }
    }
}
