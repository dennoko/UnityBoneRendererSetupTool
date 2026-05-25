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
                SetStatus("ヒューマノイドボーンが見つかりません。", StatusType.Error);
                EditorUtility.DisplayDialog("エラー", "ヒューマノイドボーンが見つかりません。", "OK");
                return;
            }

            if (BoneRendererService.SetupBoneRenderer(_avatar, bones, BoneRendererSettings.AvatarColor))
            {
                MarkSceneDirty();
                SetStatus($"{_avatar.name}: {bones.Count} 個のボーンを設定しました。", StatusType.Success);
                Debug.Log($"[BoneRendererSetup] {_avatar.name}: {bones.Count}個のヒューマノイドボーンを設定しました。");
            }
            else
            {
                SetStatus("セットアップに失敗しました。", StatusType.Error);
            }
        }

        private void SetupOutfit()
        {
            if (_outfit == null || _avatar == null)
                return;

            var avatarHasUpperChest = AvatarBoneMapper.HasUpperChest(_avatar);
            var outfitHasUpperChest = OutfitBoneMapper.OutfitHasUpperChest(_outfit);
            var upperChestMismatch = avatarHasUpperChest != outfitHasUpperChest;

            var matches = OutfitBoneMapper.GetDetailedMatches(_outfit, _avatar);
            var bones = matches
                .Where(m => m.AvatarBone != null
                    || m.HumanBone == HumanBodyBones.UpperChest
                    || (upperChestMismatch && m.HumanBone == HumanBodyBones.Chest))
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

                // 実際に除外されたボーンのみをスキップとして扱う
                // (UpperChestミスマッチ時にChest/UpperChestを意図的に含めた場合はスキップ扱いしない)
                var skipped = matches
                    .Where(m => m.AvatarBone == null
                        && m.HumanBone != HumanBodyBones.UpperChest
                        && !(upperChestMismatch && m.HumanBone == HumanBodyBones.Chest))
                    .ToList();
                if (skipped.Count > 0)
                {
                    SetStatus($"{bones.Count} 個設定 / {skipped.Count} 個スキップ", StatusType.Warning);
                    Debug.LogWarning($"[BoneRendererSetup] {skipped.Count}個のボーンはアバターにマッチしませんでした:");
                    foreach (var s in skipped)
                        Debug.LogWarning($"  - {s.OutfitBone.name} ({s.HumanBone})");
                }
                else
                {
                    SetStatus($"{_outfit.name}: {bones.Count} 個のボーンを設定しました。", StatusType.Success);
                }
            }
            else
            {
                SetStatus("セットアップに失敗しました。", StatusType.Error);
            }
        }

        private void RemoveRenderer(GameObject target)
        {
            if (target == null)
                return;

            if (BoneRendererService.RemoveBoneRenderer(target))
            {
                MarkSceneDirty();
                SetStatus($"{target.name}: BoneRenderer を削除しました。", StatusType.Success);
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
                SetStatus("シーン内に Humanoid Avatar が見つかりません。", StatusType.Error);
                EditorUtility.DisplayDialog("検索結果", "シーン内にHumanoid Avatarが見つかりません。", "OK");
                return;
            }

            if (avatars.Length == 1)
            {
                SetAvatarAndSetup(avatars[0]);
                return;
            }

            // VRC Avatar Descriptor を持つ候補を優先
            var vrcAvatars = avatars.Where(HasVRCAvatarDescriptor).ToArray();
            if (vrcAvatars.Length == 1)
            {
                SetAvatarAndSetup(vrcAvatars[0]);
                return;
            }

            // メニュー表示: VRC Avatar Descriptor を持つものを先頭に、ラベルに [VRC] を付与
            SetStatus($"{avatars.Length} 体のアバターが見つかりました。選択してください。", StatusType.Info);
            var sorted = avatars.OrderByDescending(HasVRCAvatarDescriptor).ToArray();
            var menu = new GenericMenu();
            foreach (var a in sorted)
            {
                var captured = a;
                var label = HasVRCAvatarDescriptor(a) ? $"[VRC] {a.name}" : a.name;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    SetAvatarAndSetup(captured);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private static bool HasVRCAvatarDescriptor(GameObject go)
        {
            return go.GetComponent("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor") != null
                || go.GetComponent("VRCSDK2.VRC_AvatarDescriptor") != null;
        }

        private void SetAvatarAndSetup(GameObject newAvatar)
        {
            if (_avatar != null && _avatar != newAvatar)
                RemoveRenderer(_avatar);

            _avatar = newAvatar;
            Debug.Log($"[BoneRendererSetup] アバターを検出: {_avatar.name}");

            if (CanSetupAvatar())
                SetupAvatar();
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
