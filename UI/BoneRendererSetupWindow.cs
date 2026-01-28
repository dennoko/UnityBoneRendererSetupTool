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
    public class BoneRendererSetupWindow : EditorWindow
    {
        private GameObject _avatar;
        private GameObject _outfit;

        private Vector2 _scrollPosition;

        [MenuItem("dennokoworks/BoneRendererSetupTool")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoneRendererSetupWindow>();
            window.titleContent = new GUIContent("Bone Renderer Setup");
            window.minSize = new Vector2(300, 350);
            window.Show();
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
                _avatar = (GameObject)EditorGUILayout.ObjectField(
                    _avatar, typeof(GameObject), true);
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
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
                    if (GUILayout.Button("Setup Avatar", GUILayout.Height(25)))
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
                _outfit = (GameObject)EditorGUILayout.ObjectField(
                    _outfit, typeof(GameObject), true);
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
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
                    if (GUILayout.Button("Setup Outfit", GUILayout.Height(25)))
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

        #region アクション

        private void SetupAvatar()
        {
            if (_avatar == null)
                return;

            var bones = AvatarBoneMapper.GetHumanoidBones(_avatar);
            if (bones.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "ヒューマノイドボーンが見つかりません。",
                    "OK");
                return;
            }

            if (BoneRendererService.SetupBoneRenderer(_avatar, bones, BoneRendererSettings.AvatarColor))
            {
                MarkSceneDirty();
                Debug.Log($"[BoneRendererSetup] {_avatar.name}: {bones.Count}個のヒューマノイドボーンを設定しました。");
            }
        }

        private void SetupOutfit()
        {
            if (_outfit == null || _avatar == null)
                return;

            var matches = OutfitBoneMapper.GetDetailedMatches(_outfit, _avatar);
            var bones = matches
                .Where(m => m.AvatarBone != null)
                .Select(m => m.OutfitBone)
                .ToList();

            if (bones.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "マッチするボーンが見つかりません。\n" +
                    "衣装のボーン命名規則を確認してください。",
                    "OK");
                return;
            }

            if (BoneRendererService.SetupBoneRenderer(_outfit, bones, BoneRendererSettings.OutfitColor))
            {
                MarkSceneDirty();
                Debug.Log($"[BoneRendererSetup] {_outfit.name}: {_avatar.name}を参照して{bones.Count}個のボーンを設定しました。");

                // マッチできなかったボーンがあれば警告
                var skipped = matches.Where(m => m.AvatarBone == null).ToList();
                if (skipped.Count > 0)
                {
                    Debug.LogWarning($"[BoneRendererSetup] {skipped.Count}個のボーンはアバターにマッチしませんでした:");
                    foreach (var s in skipped)
                    {
                        Debug.LogWarning($"  - {s.OutfitBone.name} ({s.HumanBone})");
                    }
                }
            }
        }

        private void RemoveRenderer(GameObject target)
        {
            if (target == null)
                return;

            if (BoneRendererService.RemoveBoneRenderer(target))
            {
                MarkSceneDirty();
                Debug.Log($"[BoneRendererSetup] {target.name}: BoneRendererを削除しました。");
            }
        }

        private void SearchAvatarInScene()
        {
            var avatars = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .Where(a => a.avatar != null && a.avatar.isHuman)
                .Select(a => a.gameObject)
                .ToArray();

            if (avatars.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "検索結果",
                    "シーン内にHumanoid Avatarが見つかりません。",
                    "OK");
                return;
            }

            if (avatars.Length == 1)
            {
                _avatar = avatars[0];
                Debug.Log($"[BoneRendererSetup] アバターを検出: {_avatar.name}");
            }
            else
            {
                var menu = new GenericMenu();
                foreach (var a in avatars)
                {
                    var captured = a;
                    menu.AddItem(new GUIContent(a.name), false, () =>
                    {
                        _avatar = captured;
                        Repaint();
                    });
                }
                menu.ShowAsContext();
            }
        }

        #endregion

        #region バリデーション

        private bool CanSetupAvatar()
        {
            return _avatar != null && AvatarBoneMapper.IsHumanoidAvatar(_avatar);
        }

        private bool CanSetupOutfit()
        {
            return _outfit != null && _avatar != null && 
                   AvatarBoneMapper.IsHumanoidAvatar(_avatar);
        }

        private bool HasBoneRenderer(GameObject target)
        {
            return target != null && BoneRendererService.HasBoneRenderer(target);
        }

        private bool HasArmature(GameObject target)
        {
            if (target == null)
                return false;

            var armatureNames = new[] { "armature", "skeleton", "root" };
            foreach (Transform child in target.transform)
            {
                var lower = child.name.ToLowerInvariant();
                if (armatureNames.Any(n => lower.Contains(n)))
                    return true;
            }
            return false;
        }

        #endregion

        private void MarkSceneDirty()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
