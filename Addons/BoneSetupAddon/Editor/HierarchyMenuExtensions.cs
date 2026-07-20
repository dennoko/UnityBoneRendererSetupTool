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

            AlignSingleBone(targetBone.transform, match.AvatarBone, matches);

            // 左右同期が有効な場合、ミラーボーンも同じ手順で Align する。
            // MA Scale Adjuster の m_Scale はローカル軸依存の量のため、値のコピーでは
            // 左右のローカルフレームが対称でないケースでボーン長がずれる。
            // ミラー側も自身の幾何情報から位置・回転・スケールを再計算することで左右差を防ぐ。
            var mirrorBone = LRSyncFeature.Instance?.TryGetMirror(targetBone.transform);
            if (mirrorBone != null)
            {
                var mirrorMatch = matches.FirstOrDefault(m => m.OutfitBone == mirrorBone);
                if (mirrorMatch.OutfitBone != null && mirrorMatch.AvatarBone != null)
                {
                    AlignSingleBone(mirrorBone, mirrorMatch.AvatarBone, matches);
                }
            }
        }

        /// <summary>
        /// 1本のボーンをアバターへ Align する（位置 → 回転 → MA Scale Adjuster）。
        /// スケールはこのボーン自身のローカル空間で計算されるため、他ボーンへの値コピーは行わないこと。
        /// </summary>
        private static void AlignSingleBone(
            Transform outfitBone, Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            Undo.RecordObject(outfitBone, "Align Bone with Avatar");

            // Align World Position
            outfitBone.position = avatarBone.position;

            // Align Rotation（スイング・ツイスト分解でねじれを防止）
            AlignBoneRotation(outfitBone, avatarBone, matches);

            // 子ボーン始点が直線上にある場合、MA Scale Adjuster で距離を揃える
            MAScaleAdjusterApplier.TryApplyScaleAdjuster(outfitBone, avatarBone, matches);

            Debug.Log($"[BoneRenderer] Aligned '{outfitBone.name}' to '{avatarBone.name}'");
        }

        /// <summary>
        /// ボーンの回転をスイング・ツイスト分解で合わせる。
        /// Step1(スイング): 子方向をアバターに合わせる。
        /// Step2(ツイスト): アバターボーンの実際のワールド回転を参照してロールを揃える。
        /// 孫ボーン位置やFBXバインドポーズに依存しないため、A-pose/T-pose差も吸収できる。
        /// </summary>
        private static void AlignBoneRotation(
            Transform outfitBone, Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            FindMatchedChild(outfitBone, matches, out var outfitChild, out var avatarChild);

            if (outfitChild == null || avatarChild == null)
                return;

            Vector3 outfitDir = outfitChild.position - outfitBone.position;
            Vector3 avatarDir = avatarChild.position - avatarBone.position;

            if (outfitDir.sqrMagnitude < 1e-6f || avatarDir.sqrMagnitude < 1e-6f)
                return;

            outfitDir.Normalize();
            avatarDir.Normalize();

            // Step1: スイング — 子ボーンの方向だけを合わせる（ロールは無視）
            Quaternion swing = Quaternion.FromToRotation(outfitDir, avatarDir);
            outfitBone.rotation = swing * outfitBone.rotation;

            // Step2: ツイスト — ロールをアバターボーンの実際のワールド回転に揃える
            // 骨方向に最も垂直なローカル軸を取得し、骨方向の平面へ投影して比較する
            Vector3 avatarPerp = Vector3.ProjectOnPlane(
                GetMostPerpendicularAxis(avatarBone, avatarDir), avatarDir);
            Vector3 outfitPerp = Vector3.ProjectOnPlane(
                GetMostPerpendicularAxis(outfitBone, avatarDir), avatarDir);

            if (avatarPerp.sqrMagnitude > 1e-6f && outfitPerp.sqrMagnitude > 1e-6f)
            {
                Quaternion twist = Quaternion.FromToRotation(
                    outfitPerp.normalized, avatarPerp.normalized);
                outfitBone.rotation = twist * outfitBone.rotation;
            }
        }

        /// <summary>
        /// ボーンのローカル軸（right/up/forward）の中で boneDir に最も垂直なものをワールド座標で返す
        /// </summary>
        private static Vector3 GetMostPerpendicularAxis(Transform bone, Vector3 boneDir)
        {
            Vector3 r = bone.right, u = bone.up, f = bone.forward;
            float dr = Mathf.Abs(Vector3.Dot(r, boneDir));
            float du = Mathf.Abs(Vector3.Dot(u, boneDir));
            float df = Mathf.Abs(Vector3.Dot(f, boneDir));
            if (dr <= du && dr <= df) return r;
            if (du <= df) return u;
            return f;
        }

        /// <summary>
        /// 指定ボーンの子の中からマッチ済みペアを探す。
        /// preferredDir が指定されている場合は、その方向と最も内積が高い子を優先する（案D）。
        /// </summary>
        private static void FindMatchedChild(
            Transform parent, List<OutfitBoneMapper.MatchResult> matches,
            out Transform outfitChild, out Transform avatarChild,
            Vector3 preferredDir = default)
        {
            outfitChild = null;
            avatarChild = null;

            bool useDir = preferredDir.sqrMagnitude > 1e-6f;
            float bestScore = float.NegativeInfinity;

            foreach (Transform child in parent)
            {
                var childMatch = matches.FirstOrDefault(m => m.OutfitBone == child);
                if (childMatch.OutfitBone == null || childMatch.AvatarBone == null) continue;

                float score = useDir
                    ? Vector3.Dot((child.position - parent.position).normalized, preferredDir)
                    : 0f;

                if (outfitChild == null || score > bestScore)
                {
                    bestScore = score;
                    outfitChild = childMatch.OutfitBone;
                    avatarChild = childMatch.AvatarBone;
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
