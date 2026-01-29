using System.Linq;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hays.BoneRendererSetup.UI
{
    /// <summary>
    /// BoneRendererSetupWindow - アクション処理
    /// </summary>
    public partial class BoneRendererSetupWindow
    {
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
