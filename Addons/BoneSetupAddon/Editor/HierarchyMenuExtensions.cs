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
        /// 1本のボーンをアバターへ Align する（位置 → 回転）。
        /// スケールはこのボーン自身のローカル空間で計算されるため、他ボーンへの値コピーは行わないこと。
        /// </summary>
        private static void AlignSingleBone(
            Transform outfitBone, Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            Undo.RecordObject(outfitBone, "Align Bone with Avatar");

            // Align World Position
            outfitBone.position = avatarBone.position;

            // Align Rotation（軸入れ替え候補の中から最小回転を選びねじれを防止）
            AlignBoneRotation(outfitBone, avatarBone, matches);

            // MA Scale Adjuster への自動数値設定は現在無効化されています。
            // 機能を有効に戻す場合は以下のコメントアウトを解除してください。
            // MAScaleAdjusterApplier.TryApplyScaleAdjuster(outfitBone, avatarBone, matches);

            Debug.Log($"[BoneRenderer] Aligned '{outfitBone.name}' to '{avatarBone.name}'");
        }

        /// <summary>
        /// ボーンの回転をアバターへ合わせる。
        /// 衣装とアバターは同じ向きでシーンに置かれている前提で、
        /// 「3 軸すべてがアバターの軸と平行になる回転」の中から回転量が最小のものを選ぶ。
        /// 座標系（軸の割り当て規約）の違いを吸収しつつ、余計なロール＝ねじれが入らない。
        /// 詳細は <see cref="BoneRotationAligner"/> を参照。
        /// </summary>
        private static void AlignBoneRotation(
            Transform outfitBone, Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            BoneRotationAligner.FindMatchedChild(
                outfitBone, matches, out var outfitChild, out var avatarChild);

            // 子ボーンが無い（末端ボーンなど）場合も、軸の平行化だけは行う
            outfitBone.rotation = BoneRotationAligner.Solve(
                outfitBone, avatarBone, outfitChild, avatarChild);
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
