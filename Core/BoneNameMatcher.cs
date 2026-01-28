using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hays.BoneRendererSetup.Core
{
    /// <summary>
    /// ボーン名の正規化とマッチングを行うユーティリティクラス
    /// </summary>
    public static class BoneNameMatcher
    {
        private static readonly Regex EndNumberPattern = new Regex(@"[_\.][0-9]+$");
        private static readonly Regex VrmBonePattern = new Regex(@"^([LRC])_(.*)$");
        private static readonly Regex SideSuffixPattern = new Regex(@"[_\.]([LR])$", RegexOptions.IgnoreCase);

        /// <summary>
        /// ボーン名を正規化する
        /// 大文字小文字、数字、スペース、アンダースコア、ドットを統一的に処理
        /// </summary>
        public static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            // 小文字化
            name = name.ToLowerInvariant();

            // "Bone_" プレフィックスを除去
            if (name.StartsWith("bone_"))
                name = name.Substring(5);

            // 数字、スペース、アンダースコア、ドットを除去
            name = Regex.Replace(name, @"[0-9 ._]", "");

            return name;
        }

        /// <summary>
        /// ボーン名からHumanBodyBonesを推定する
        /// </summary>
        /// <param name="boneName">対象のボーン名</param>
        /// <returns>マッチしたボーン、見つからない場合はnull</returns>
        public static HumanBodyBones? TryMatchBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return null;

            var normalized = NormalizeBoneName(boneName);

            if (HumanoidBonePatterns.NameToBoneMap.TryGetValue(normalized, out var bones) && bones.Count > 0)
            {
                return bones[0];
            }

            return null;
        }

        /// <summary>
        /// ボーン名からマッチする可能性のある全てのHumanBodyBonesを取得
        /// </summary>
        public static List<HumanBodyBones> GetPossibleBones(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return new List<HumanBodyBones>();

            var normalized = NormalizeBoneName(boneName);

            if (HumanoidBonePatterns.NameToBoneMap.TryGetValue(normalized, out var bones))
            {
                return new List<HumanBodyBones>(bones);
            }

            return new List<HumanBodyBones>();
        }

        /// <summary>
        /// 正規化されたボーン名がヒューマノイドボーンとして認識可能かどうか
        /// </summary>
        public static bool IsRecognizedBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return false;

            var normalized = NormalizeBoneName(boneName);
            return HumanoidBonePatterns.AllNormalizedBoneNames.Contains(normalized);
        }

        /// <summary>
        /// ボーン名からプレフィックスとサフィックスを推定する
        /// </summary>
        /// <param name="boneName">対象のボーン名</param>
        /// <param name="knownBonePart">既知のボーン部分（例: "Hips"）</param>
        /// <param name="prefix">推定されたプレフィックス</param>
        /// <param name="suffix">推定されたサフィックス</param>
        /// <returns>推定成功した場合true</returns>
        public static bool TryInferPrefixSuffix(string boneName, string knownBonePart, 
            out string prefix, out string suffix)
        {
            prefix = string.Empty;
            suffix = string.Empty;

            if (string.IsNullOrEmpty(boneName) || string.IsNullOrEmpty(knownBonePart))
                return false;

            // 大文字小文字を無視して検索
            int index = boneName.IndexOf(knownBonePart, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            prefix = boneName.Substring(0, index);
            suffix = boneName.Substring(index + knownBonePart.Length);
            return true;
        }

        /// <summary>
        /// プレフィックスとサフィックスを適用してターゲット名を生成
        /// </summary>
        public static string ApplyPrefixSuffix(string baseName, string prefix, string suffix)
        {
            return prefix + baseName + suffix;
        }

        /// <summary>
        /// プレフィックスとサフィックスを除去してベース名を取得
        /// </summary>
        public static string StripPrefixSuffix(string boneName, string prefix, string suffix)
        {
            if (string.IsNullOrEmpty(boneName))
                return boneName;

            if (!string.IsNullOrEmpty(prefix) && boneName.StartsWith(prefix))
            {
                boneName = boneName.Substring(prefix.Length);
            }

            if (!string.IsNullOrEmpty(suffix) && boneName.EndsWith(suffix))
            {
                boneName = boneName.Substring(0, boneName.Length - suffix.Length);
            }

            return boneName;
        }

        /// <summary>
        /// ボーン名が左右のサイド情報を含むかどうかを判定
        /// </summary>
        public static bool HasSideIndicator(string boneName, out bool isLeft)
        {
            isLeft = false;
            if (string.IsNullOrEmpty(boneName))
                return false;

            var lower = boneName.ToLowerInvariant();

            // Left/Rightの文字列を含むか
            if (lower.Contains("left") || lower.Contains("_l") || lower.EndsWith(".l") || 
                lower.StartsWith("l_") || lower.StartsWith("l."))
            {
                isLeft = true;
                return true;
            }

            if (lower.Contains("right") || lower.Contains("_r") || lower.EndsWith(".r") || 
                lower.StartsWith("r_") || lower.StartsWith("r."))
            {
                isLeft = false;
                return true;
            }

            return false;
        }
    }
}
