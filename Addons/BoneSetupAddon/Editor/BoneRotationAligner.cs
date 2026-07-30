using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hays.BoneRendererSetup.Matching;

namespace Hays.BoneRendererSetup.Addons
{
    /// <summary>
    /// 衣装ボーンをアバターボーンへ合わせるためのワールド回転を求めるソルバー。
    ///
    /// 【前提】アバターと衣装はシーンのワールド空間内でほぼ同じ向きに置かれている。
    /// このときねじれ（意図しない 90°/180° のロール）は、衣装とアバターで
    /// ボーンのローカル軸の割り当て規約が異なること（どの軸が骨方向か、
    /// どの軸がロール基準か、軸の符号）が原因で発生する。
    ///
    /// 【解法】出力回転の候補を「アバターボーンのワールド回転 × 軸入れ替え回転(24通り)」に限定する。
    /// 軸入れ替え回転は、XYZ 軸を符号付きで並べ替える正の直交行列（立方体の回転対称群）である。
    ///   - どの候補を選んでも、衣装ボーンの XYZ 軸はアバターボーンの XYZ 軸のいずれかと必ず平行になる
    ///     → 座標系の規約差をそのまま吸収できる
    ///   - その中で「現在の衣装ボーンの回転からの回転量が最小」のものを選ぶ
    ///     → ワールド空間で既に同じ向きを向いている以上、余計なロールは一切入らない
    ///
    /// 骨方向（子ボーンへの向き）が分かる場合は、骨方向を取り違えた候補を事前に棄却する。
    /// これにより A ポーズ / T ポーズ差のように回転差が 45°を超えるケースでも正しい軸対応を選べる。
    /// 残るロール方向の曖昧さは「回転量最小」で解消される。
    /// </summary>
    internal static class BoneRotationAligner
    {
        /// <summary>骨方向がこれ以上ずれる候補は、軸の取り違えとみなして棄却する</summary>
        private const float DirectionRejectAngle = 45f;

        /// <summary>最良候補の骨方向誤差からこの範囲内の候補のみを許容する（衣装固有の骨の傾きを吸収する余裕）</summary>
        private const float DirectionSlackAngle = 20f;

        private const float MinSqrLength = 1e-10f;

        /// <summary>XYZ 軸を符号付きで入れ替える 24 通りの回転（立方体の回転対称群）</summary>
        private static readonly Quaternion[] AxisPermutations = BuildAxisPermutations();

        /// <summary>
        /// 衣装ボーンに設定すべきワールド回転を求める。
        /// </summary>
        /// <param name="outfitRotation">衣装ボーンの現在のワールド回転</param>
        /// <param name="avatarRotation">アバターボーンのワールド回転</param>
        /// <param name="outfitBoneDirWorld">衣装ボーンの骨方向（ワールド）。不明なら Vector3.zero</param>
        /// <param name="avatarBoneDirWorld">アバターボーンの骨方向（ワールド）。不明なら Vector3.zero</param>
        public static Quaternion Solve(
            Quaternion outfitRotation,
            Quaternion avatarRotation,
            Vector3 outfitBoneDirWorld,
            Vector3 avatarBoneDirWorld)
        {
            bool useDirection =
                outfitBoneDirWorld.sqrMagnitude > MinSqrLength &&
                avatarBoneDirWorld.sqrMagnitude > MinSqrLength;

            if (!useDirection)
                return SolveMinimalRotation(outfitRotation, avatarRotation);

            Vector3 avatarDir = avatarBoneDirWorld.normalized;
            // 衣装ボーンのローカル系で見た骨方向。候補回転を適用した後のワールド方向を再現するために使う。
            Vector3 outfitLocalDir = Quaternion.Inverse(outfitRotation) * outfitBoneDirWorld.normalized;

            // 到達可能な最小の骨方向誤差を求め、そこから一定範囲内の候補だけを許容する
            float minDirError = float.PositiveInfinity;
            foreach (var permutation in AxisPermutations)
            {
                float error = Vector3.Angle(avatarRotation * permutation * outfitLocalDir, avatarDir);
                if (error < minDirError) minDirError = error;
            }

            float tolerance = Mathf.Min(DirectionRejectAngle, minDirError + DirectionSlackAngle);

            Quaternion best = outfitRotation;
            float bestDelta = float.PositiveInfinity;

            foreach (var permutation in AxisPermutations)
            {
                Quaternion candidate = avatarRotation * permutation;

                if (Vector3.Angle(candidate * outfitLocalDir, avatarDir) > tolerance)
                    continue;

                float delta = Quaternion.Angle(outfitRotation, candidate);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = candidate;
                }
            }

            // どの候補も骨方向を再現できない（衣装の骨構造がアバターと大きく異なる）場合は
            // 骨方向を使わず、純粋な最小回転にフォールバックする
            if (float.IsPositiveInfinity(bestDelta))
                return SolveMinimalRotation(outfitRotation, avatarRotation);

            return best;
        }

        /// <summary>
        /// Transform から骨方向を取り出して <see cref="Solve"/> を呼ぶユーティリティ。
        /// </summary>
        public static Quaternion Solve(
            Transform outfitBone, Transform avatarBone,
            Transform outfitChild, Transform avatarChild)
        {
            Vector3 outfitDir = outfitChild != null
                ? outfitChild.position - outfitBone.position
                : Vector3.zero;
            Vector3 avatarDir = avatarChild != null
                ? avatarChild.position - avatarBone.position
                : Vector3.zero;

            return Solve(outfitBone.rotation, avatarBone.rotation, outfitDir, avatarDir);
        }

        /// <summary>
        /// 3 軸が平行になる候補のうち、現在の回転からの回転量が最小のものを返す。
        /// </summary>
        private static Quaternion SolveMinimalRotation(Quaternion outfitRotation, Quaternion avatarRotation)
        {
            Quaternion best = outfitRotation;
            float bestDelta = float.PositiveInfinity;

            foreach (var permutation in AxisPermutations)
            {
                Quaternion candidate = avatarRotation * permutation;
                float delta = Quaternion.Angle(outfitRotation, candidate);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 符号付き軸入れ替え回転 24 通りを生成する。
        /// forward に 6 方向、up にそれと直交する 4 方向を割り当てると、
        /// right は外積で決まり、鏡映を含まない正の回転のみが得られる。
        /// </summary>
        private static Quaternion[] BuildAxisPermutations()
        {
            var axes = new[]
            {
                Vector3.right, Vector3.left,
                Vector3.up, Vector3.down,
                Vector3.forward, Vector3.back
            };

            var result = new List<Quaternion>(24);
            foreach (var forward in axes)
            {
                foreach (var up in axes)
                {
                    // 同じ軸（平行・反平行）同士の組み合わせは回転を構成できない
                    if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.5f)
                        continue;

                    result.Add(Quaternion.LookRotation(forward, up));
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 指定ボーンの子の中からマッチ済みのペアを探す（最初に見つかったもの）。
        /// 回転計算と MA Scale Adjuster で同じ子ボーンを参照するため、両者でこの実装を共有する。
        /// </summary>
        public static void FindMatchedChild(
            Transform parent,
            List<OutfitBoneMapper.MatchResult> matches,
            out Transform outfitChild,
            out Transform avatarChild)
        {
            outfitChild = null;
            avatarChild = null;

            foreach (Transform child in parent)
            {
                var match = matches.FirstOrDefault(m => m.OutfitBone == child);
                if (match.OutfitBone == null || match.AvatarBone == null)
                    continue;

                outfitChild = match.OutfitBone;
                avatarChild = match.AvatarBone;
                return;
            }
        }
    }
}
