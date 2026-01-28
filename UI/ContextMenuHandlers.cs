using System.Linq;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Hays.BoneRendererSetup.UI
{
    /// <summary>
    /// Hierarchyコンテキストメニューのハンドラー
    /// </summary>
    public static class ContextMenuHandlers
    {
        private const string MenuBase = "GameObject/BoneRendererSetupTool/";
        
        // アバター用メニュー
        private const string MenuSetupAvatar = MenuBase + "Setup (Avatar)";
        private const string MenuRemoveAvatar = MenuBase + "Remove (Avatar)";
        
        // 衣装用メニュー
        private const string MenuSetupOutfit = MenuBase + "Setup (Outfit → Avatar)";
        private const string MenuRemoveOutfit = MenuBase + "Remove (Outfit)";
        
        #region アバター用メニュー

        [MenuItem(MenuSetupAvatar, false, 10)]
        private static void SetupAvatar()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bone Renderer Setup Tool",
                    "Humanoid Animatorを持つアバターを選択してください。",
                    "OK");
                return;
            }

            int processed = 0;
            foreach (var go in selected)
            {
                if (!AvatarBoneMapper.IsHumanoidAvatar(go))
                {
                    Debug.LogWarning($"[BoneRendererSetup] {go.name}: Humanoid Avatarではありません。");
                    continue;
                }

                var bones = AvatarBoneMapper.GetHumanoidBones(go);
                if (bones.Count == 0)
                {
                    Debug.LogWarning($"[BoneRendererSetup] {go.name}: ヒューマノイドボーンが見つかりません。");
                    continue;
                }

                if (BoneRendererService.SetupBoneRenderer(go, bones, BoneRendererSettings.AvatarColor))
                {
                    processed++;
                    Debug.Log($"[BoneRendererSetup] {go.name}: {bones.Count}個のヒューマノイドボーンを設定しました。");
                }
            }

            if (processed > 0)
            {
                MarkSceneDirty();
            }
        }

        [MenuItem(MenuSetupAvatar, true)]
        private static bool ValidateSetupAvatar()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            return selected.Any(go => AvatarBoneMapper.IsHumanoidAvatar(go));
        }

        [MenuItem(MenuRemoveAvatar, false, 11)]
        private static void RemoveAvatarRenderer()
        {
            RemoveSelectedRenderers();
        }

        [MenuItem(MenuRemoveAvatar, true)]
        private static bool ValidateRemoveAvatarRenderer()
        {
            return ValidateHasBoneRenderer();
        }

        #endregion

        #region 衣装用メニュー

        [MenuItem(MenuSetupOutfit, false, 20)]
        private static void SetupOutfit()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bone Renderer Setup Tool",
                    "衣装のGameObjectを選択してください。",
                    "OK");
                return;
            }

            // シーン内のアバターを検索
            var avatars = FindHumanoidAvatarsInScene();
            if (avatars.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bone Renderer Setup Tool",
                    "シーン内にHumanoid Avatarが見つかりません。\n先にアバターをシーンに配置してください。",
                    "OK");
                return;
            }

            // アバターが1つの場合は自動選択、複数の場合は選択ダイアログ
            GameObject avatar = null;
            if (avatars.Length == 1)
            {
                avatar = avatars[0];
            }
            else
            {
                // 選択ダイアログを表示
                var menu = new GenericMenu();
                foreach (var a in avatars)
                {
                    var captured = a;
                    menu.AddItem(new GUIContent(a.name), false, () =>
                    {
                        SetupOutfitWithAvatar(selected, captured);
                    });
                }
                menu.ShowAsContext();
                return;
            }

            SetupOutfitWithAvatar(selected, avatar);
        }

        private static void SetupOutfitWithAvatar(GameObject[] outfits, GameObject avatar)
        {
            int processed = 0;
            foreach (var outfit in outfits)
            {
                var bones = OutfitBoneMapper.MatchToAvatar(outfit, avatar);
                if (bones.Count == 0)
                {
                    Debug.LogWarning($"[BoneRendererSetup] {outfit.name}: マッチするボーンが見つかりません。");
                    continue;
                }

                if (BoneRendererService.SetupBoneRenderer(outfit, bones, BoneRendererSettings.OutfitColor))
                {
                    processed++;
                    Debug.Log($"[BoneRendererSetup] {outfit.name}: {avatar.name}を参照して{bones.Count}個のボーンを設定しました。");
                }
            }

            if (processed > 0)
            {
                MarkSceneDirty();
            }
        }

        [MenuItem(MenuSetupOutfit, true)]
        private static bool ValidateSetupOutfit()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            // 選択されたオブジェクトがアバターでなく、かつSMRまたはArmatureを持つか
            return selected.Any(go => 
                !AvatarBoneMapper.IsHumanoidAvatar(go) &&
                (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                 HasArmatureLikeChild(go)));
        }

        [MenuItem(MenuRemoveOutfit, false, 21)]
        private static void RemoveOutfitRenderer()
        {
            RemoveSelectedRenderers();
        }

        [MenuItem(MenuRemoveOutfit, true)]
        private static bool ValidateRemoveOutfitRenderer()
        {
            return ValidateHasBoneRenderer();
        }

        #endregion

        #region ヘルパー

        private static void RemoveSelectedRenderers()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Remove Bone Renderer",
                    "BoneRendererを持つGameObjectを選択してください。",
                    "OK");
                return;
            }

            int removed = 0;
            foreach (var go in selected)
            {
                if (BoneRendererService.RemoveBoneRenderer(go))
                {
                    removed++;
                    Debug.Log($"[BoneRendererSetup] {go.name}: BoneRendererを削除しました。");
                }
            }

            if (removed > 0)
            {
                MarkSceneDirty();
            }
            else
            {
                Debug.Log("[BoneRendererSetup] 削除するBoneRendererがありませんでした。");
            }
        }

        private static bool ValidateHasBoneRenderer()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            return selected.Any(go => go.GetComponent<BoneRenderer>() != null);
        }

        private static GameObject[] FindHumanoidAvatarsInScene()
        {
            return Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .Where(a => a.avatar != null && a.avatar.isHuman)
                .Select(a => a.gameObject)
                .ToArray();
        }

        private static bool HasArmatureLikeChild(GameObject go)
        {
            var armatureNames = new[] { "armature", "skeleton", "root" };
            foreach (Transform child in go.transform)
            {
                var lower = child.name.ToLowerInvariant();
                if (armatureNames.Any(n => lower.Contains(n)))
                    return true;
            }
            return false;
        }

        private static void MarkSceneDirty()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        #endregion
    }
}
