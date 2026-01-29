using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// MA Scale Presets機能
    /// スケール調整値をプリセットとして保存・適用する
    /// </summary>
    public class MAPresetFeature
    {
        private string _presetTitle = "";

        public string PresetTitle
        {
            get => _presetTitle;
            set => _presetTitle = value;
        }

        public void DrawUI(GameObject outfit)
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
            var transforms = outfit.GetComponentsInChildren<Transform>(true);
            
            var preset = ScriptableObject.CreateInstance<BoneScalePreset>();
            preset.Title = title;
            
            foreach (var t in transforms)
            {
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
                Debug.LogWarning("[MAPresetFeature] No bones with modified scale found.");
                return;
            }

            string folder = "Assets/BoneRendererData/Presets";
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            
            string filename = string.Join("_", title.Split(System.IO.Path.GetInvalidFileNameChars()));
            string path = $"{folder}/{filename}.asset";
            
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MAPresetFeature] Preset '{title}' saved to {path}");
        }

        private void ApplyPreset(GameObject outfit, BoneScalePreset preset)
        {
            var maType = TypeCache.GetTypesDerivedFrom(typeof(Component))
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster");
            
            if (maType == null)
            {
                Debug.LogError("[MAPresetFeature] ModularAvatarScaleAdjuster class not found. Is Modular Avatar installed?");
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
                
                var component = target.GetComponent(maType);
                if (component == null)
                {
                    component = Undo.AddComponent(target.gameObject, maType);
                }
                
                var so = new SerializedObject(component);
                var scaleProp = so.FindProperty("scale");
                if (scaleProp != null)
                {
                    so.Update();
                    scaleProp.vector3Value = data.Scale;
                    so.ApplyModifiedProperties();
                }
                
                appliedCount++;
            }
            
            Debug.Log($"[MAPresetFeature] Applied preset '{preset.Title}' to {appliedCount} bones.");
        }
    }
}
