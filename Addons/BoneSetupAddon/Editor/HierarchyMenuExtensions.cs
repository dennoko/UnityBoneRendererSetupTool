using UnityEditor;
using UnityEngine;
using Hays.BoneRendererSetup.UI;
using Hays.BoneRendererSetup.Matching;
using System.Collections.Generic;
using System.Linq;

namespace Hays.BoneRendererSetup.Addons
{
    public static class AddonContextMenus
    {
        [MenuItem("GameObject/BoneRenderer Setup/Align with Avatar", false, 10)]
        public static void AlignWithAvatar(MenuCommand command)
        {
            var targetBone = command.context as GameObject;
            if (targetBone == null) return;

            var window = BoneRendererSetupWindow.Instance;
            if (window == null)
            {
                Debug.LogWarning("[BoneRenderer] Setup Window is not open.");
                return;
            }

            var avatar = window.CurrentAvatar;
            var outfit = window.CurrentOutfit;

            if (avatar == null || outfit == null)
            {
                Debug.LogWarning("[BoneRenderer] Avatar or Outfit is not set in the window.");
                return;
            }

            // Verify the target bone belongs to the outfit
            if (!targetBone.transform.IsChildOf(outfit.transform) && targetBone != outfit)
            {
                 Debug.LogWarning($"[BoneRenderer] Selected object '{targetBone.name}' is not part of the active outfit '{outfit.name}'.");
                 return;
            }

            // Find matching bone in Avatar
            var matches = OutfitBoneMapper.GetDetailedMatches(outfit, avatar);
            var match = matches.FirstOrDefault(m => m.OutfitBone == targetBone.transform);

            if (match.OutfitBone == null || match.AvatarBone == null)
            {
                Debug.LogWarning($"[BoneRenderer] No matching avatar bone found for '{targetBone.name}'.");
                return;
            }

            Undo.RecordObject(targetBone.transform, "Align Bone with Avatar");
            
            // Align World Position
            targetBone.transform.position = match.AvatarBone.position;

            // Align Rotation（座標フレームベースでねじれを防止）
            AlignBoneRotation(targetBone.transform, match.AvatarBone, matches);
            
            Debug.Log($"[BoneRenderer] Aligned '{targetBone.name}' to '{match.AvatarBone.name}'");
        }

        /// <summary>
        /// ボーンの回転を合わせる。
        /// 子ボーン・孫ボーンの方向から座標フレームを構築し、ねじれなく回転を揃える。
        /// フォールバック: 子ボーンのみ → 方向合わせ（ロール保持）、子なし → 回転変更なし
        /// </summary>
        private static void AlignBoneRotation(
            Transform outfitBone, Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            // マッチ済みの子ボーンペアを探す
            FindMatchedChild(outfitBone, matches, out var outfitChild, out var avatarChild);

            if (outfitChild == null || avatarChild == null)
            {
                // 子ボーンなし: 回転は変更しない（位置のみ）
                return;
            }

            Vector3 outfitDir = outfitChild.position - outfitBone.position;
            Vector3 avatarDir = avatarChild.position - avatarBone.position;

            if (outfitDir.sqrMagnitude < 1e-6f || avatarDir.sqrMagnitude < 1e-6f)
                return;

            outfitDir.Normalize();
            avatarDir.Normalize();

            // マッチ済みの孫ボーンペアを探す（3軸決定用）
            FindMatchedChild(outfitChild, matches, out var outfitGrandChild, out var avatarGrandChild);

            if (outfitGrandChild != null && avatarGrandChild != null)
            {
                // 3軸決定: 子方向 + 曲がり平面の法線から座標フレームを構築
                Vector3 outfitDir2 = (outfitGrandChild.position - outfitChild.position);
                Vector3 avatarDir2 = (avatarGrandChild.position - avatarChild.position);

                if (outfitDir2.sqrMagnitude > 1e-6f && avatarDir2.sqrMagnitude > 1e-6f)
                {
                    outfitDir2.Normalize();
                    avatarDir2.Normalize();

                    // 曲がり平面の法線（外積）で2軸目を決定
                    Vector3 outfitNormal = Vector3.Cross(outfitDir, outfitDir2);
                    Vector3 avatarNormal = Vector3.Cross(avatarDir, avatarDir2);

                    if (outfitNormal.sqrMagnitude > 1e-6f && avatarNormal.sqrMagnitude > 1e-6f)
                    {
                        outfitNormal.Normalize();
                        avatarNormal.Normalize();

                        // 各座標フレームを構築
                        Quaternion outfitFrame = Quaternion.LookRotation(outfitDir, outfitNormal);
                        Quaternion avatarFrame = Quaternion.LookRotation(avatarDir, avatarNormal);

                        // 補正回転: outfitのフレーム → avatarのフレームへの変換
                        Quaternion correction = avatarFrame * Quaternion.Inverse(outfitFrame);
                        outfitBone.rotation = correction * outfitBone.rotation;
                        return;
                    }
                }
            }

            // フォールバック: 1軸のみ（子方向を合わせ、ロールは保持）
            Quaternion dirCorrection = Quaternion.FromToRotation(outfitDir, avatarDir);
            outfitBone.rotation = dirCorrection * outfitBone.rotation;
        }

        /// <summary>
        /// 指定ボーンの子の中からマッチ済みペアを探す
        /// </summary>
        private static void FindMatchedChild(
            Transform parent, List<OutfitBoneMapper.MatchResult> matches,
            out Transform outfitChild, out Transform avatarChild)
        {
            outfitChild = null;
            avatarChild = null;

            foreach (Transform child in parent)
            {
                var childMatch = matches.FirstOrDefault(m => m.OutfitBone == child);
                if (childMatch.OutfitBone != null && childMatch.AvatarBone != null)
                {
                    outfitChild = childMatch.OutfitBone;
                    avatarChild = childMatch.AvatarBone;
                    return;
                }
            }
        }

        [MenuItem("GameObject/BoneRenderer Setup/Align with Avatar", true)]
        public static bool ValidateAlignWithAvatar()
        {
            // Only show if Window is open and references are set
            return BoneRendererSetupWindow.Instance != null && 
                   BoneRendererSetupWindow.Instance.CurrentAvatar != null &&
                   BoneRendererSetupWindow.Instance.CurrentOutfit != null &&
                   Selection.activeGameObject != null;
        }

    }
}
