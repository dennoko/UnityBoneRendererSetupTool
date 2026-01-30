using System.Linq;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hays.BoneRendererSetup.UI
{
    /// <summary>
    /// Bone Renderer Setup Tool の EditorWindow
    /// </summary>
    public partial class BoneRendererSetupWindow : EditorWindow
    {
        private GameObject _avatar;
        private GameObject _outfit;
        private System.Collections.Generic.List<IAddonFeature> _addons = new System.Collections.Generic.List<IAddonFeature>();

        // 公開プロパティ（Addonからのアクセス用）
        public static BoneRendererSetupWindow Instance { get; private set; }
        public GameObject CurrentAvatar => _avatar;
        public GameObject CurrentOutfit => _outfit;


        private Vector2 _scrollPosition;

        [MenuItem("dennokoworks/BoneRendererSetupTool")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoneRendererSetupWindow>();
            window.titleContent = new GUIContent("Bone Renderer Setup");
            window.minSize = new Vector2(300, 350);
            window.Show();
        }

        private void OnEnable()
        {
            Instance = this;
            LoadAddons();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            foreach (var addon in _addons)
            {
                addon.OnDisable();
            }
            if (Instance == this) Instance = null;
        }

        private void LoadAddons()
        {
            _addons.Clear();
            var interfaceType = typeof(IAddonFeature);
            var types = TypeCache.GetTypesDerivedFrom(interfaceType);
            
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                
                try
                {
                    var addon = (IAddonFeature)System.Activator.CreateInstance(type);
                    addon.OnEnable();
                    _addons.Add(addon);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BoneRendererSetup] Failed to load addon {type.Name}: {e.Message}");
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            foreach (var addon in _addons)
            {
                addon.OnSceneGUI(sceneView);
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawSettingsSection();
            EditorGUILayout.Space(15);
            
            DrawAvatarSection();
            EditorGUILayout.Space(15);
            
            DrawOutfitSection();
            EditorGUILayout.Space(15);
            
            DrawAddonsSection();
            EditorGUILayout.Space(15);
            
            DrawUtilitySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Bone Renderer Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "アバターや衣装のヒューマノイドボーンにBoneRendererを設定します。\n" +
                "衣装は設定されたアバターを参照してボーンをマッチングします。",
                MessageType.Info);
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("カラー設定", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var avatarColor = EditorGUILayout.ColorField("Avatar Color", BoneRendererSettings.AvatarColor);
                if (EditorGUI.EndChangeCheck())
                {
                    BoneRendererSettings.AvatarColor = avatarColor;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var outfitColor = EditorGUILayout.ColorField("Outfit Color", BoneRendererSettings.OutfitColor);
                if (EditorGUI.EndChangeCheck())
                {
                    BoneRendererSettings.OutfitColor = outfitColor;
                }
            }
        }

        private void DrawAvatarSection()
        {
            EditorGUILayout.LabelField("アバター", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var newAvatar = (GameObject)EditorGUILayout.ObjectField(
                    _avatar, typeof(GameObject), true);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // 入力が変更された場合
                    if (_avatar != null && newAvatar != _avatar)
                    {
                        // 前のアバターからRendererを削除
                        RemoveRenderer(_avatar);
                    }
                    
                    _avatar = newAvatar;
                    
                    if (_avatar != null && CanSetupAvatar())
                    {
                        // 新しいアバターを自動Setup
                        SetupAvatar();
                    }
                }
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    if (_avatar != null)
                    {
                        RemoveRenderer(_avatar);
                    }
                    _avatar = null;
                }
            }

            // バリデーション表示
            if (_avatar != null)
            {
                if (AvatarBoneMapper.IsHumanoidAvatar(_avatar))
                {
                    var boneCount = AvatarBoneMapper.GetHumanoidBones(_avatar).Count;
                    var hasUpperChest = AvatarBoneMapper.HasUpperChest(_avatar);
                    EditorGUILayout.HelpBox(
                        $"✓ Humanoid Avatar ({boneCount}ボーン)\n" +
                        $"  UpperChest: {(hasUpperChest ? "あり" : "なし")}",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "⚠ Humanoid Avatarではありません",
                        MessageType.Warning);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanSetupAvatar()))
                {
                    if (GUILayout.Button("Setup", GUILayout.Height(25)))
                    {
                        SetupAvatar();
                    }
                }

                using (new EditorGUI.DisabledScope(!HasBoneRenderer(_avatar)))
                {
                    if (GUILayout.Button("Remove Renderer", GUILayout.Height(25)))
                    {
                        RemoveRenderer(_avatar);
                    }
                }
            }
        }

        private void DrawOutfitSection()
        {
            EditorGUILayout.LabelField("衣装", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var newOutfit = (GameObject)EditorGUILayout.ObjectField(
                    _outfit, typeof(GameObject), true);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // 入力が変更された場合
                    if (_outfit != null && newOutfit != _outfit)
                    {
                        // 前の衣装からRendererを削除
                        RemoveRenderer(_outfit);
                    }
                    
                    _outfit = newOutfit;
                    
                    if (_outfit != null && CanSetupOutfit())
                    {
                        // 新しい衣装を自動Setup
                        SetupOutfit();
                    }
                }
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    if (_outfit != null)
                    {
                        RemoveRenderer(_outfit);
                    }
                    _outfit = null;
                }
            }

            // 情報表示
            if (_outfit != null)
            {
                var smrCount = _outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                var hasArmature = HasArmature(_outfit);
                
                EditorGUILayout.HelpBox(
                    $"SkinnedMeshRenderer: {smrCount}個\n" +
                    $"Armature: {(hasArmature ? "検出" : "未検出")}",
                    MessageType.None);
            }

            // アバター参照が必要な場合の警告
            if (_outfit != null && _avatar == null)
            {
                EditorGUILayout.HelpBox(
                    "衣装のセットアップにはアバターの参照が必要です",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanSetupOutfit()))
                {
                    if (GUILayout.Button("Setup", GUILayout.Height(25)))
                    {
                        SetupOutfit();
                    }
                }

                using (new EditorGUI.DisabledScope(!HasBoneRenderer(_outfit)))
                {
                    if (GUILayout.Button("Remove Renderer", GUILayout.Height(25)))
                    {
                        RemoveRenderer(_outfit);
                    }
                }
            }
        }

        private void DrawAddonsSection()
        {
            if (_addons.Count == 0) return;

            EditorGUILayout.LabelField("Addons", EditorStyles.boldLabel);
            foreach (var addon in _addons)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(addon.DisplayName, EditorStyles.miniBoldLabel);
                addon.OnGUI(_avatar, _outfit);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        private void DrawUtilitySection()
        {
            EditorGUILayout.LabelField("ユーティリティ", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("シーンからアバターを検索"))
                {
                    SearchAvatarInScene();
                }
            }
    }
}
}
