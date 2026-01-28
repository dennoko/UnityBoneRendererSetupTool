using System;
using System.Collections.Generic;
using System.Linq;
using Hays.BoneRendererSetup.Core;
using UnityEngine;

namespace Hays.BoneRendererSetup.Matching
{
    /// <summary>
    /// 衣装のボーンをアバターのヒューマノイドボーンにマッチングするクラス
    /// </summary>
    public static class OutfitBoneMapper
    {
        /// <summary>
        /// マッチング結果を表す構造体
        /// </summary>
        public struct MatchResult
        {
            public Transform OutfitBone;
            public Transform AvatarBone;
            public HumanBodyBones HumanBone;
            public float Confidence; // 0.0-1.0
        }

        /// <summary>
        /// 衣装のボーンをアバターにマッチングする
        /// </summary>
        /// <param name="outfitRoot">衣装のルートGameObject（Armatureを含む）</param>
        /// <param name="avatarRoot">アバターのルートGameObject</param>
        /// <returns>マッチしたボーンのTransformリスト</returns>
        public static List<Transform> MatchToAvatar(GameObject outfitRoot, GameObject avatarRoot)
        {
            if (outfitRoot == null || avatarRoot == null)
                return new List<Transform>();

            var matches = GetDetailedMatches(outfitRoot, avatarRoot);
            return matches.Select(m => m.OutfitBone).ToList();
        }

        /// <summary>
        /// 詳細なマッチング結果を取得
        /// </summary>
        public static List<MatchResult> GetDetailedMatches(GameObject outfitRoot, GameObject avatarRoot)
        {
            var results = new List<MatchResult>();

            if (outfitRoot == null || avatarRoot == null)
                return results;

            var animator = AvatarBoneMapper.GetAnimator(avatarRoot);
            if (animator == null)
                return results;

            // アバターのボーンマッピングを取得
            var avatarBoneMap = AvatarBoneMapper.GetBoneMapping(avatarRoot);
            var avatarHasUpperChest = AvatarBoneMapper.HasUpperChest(avatarRoot);

            // 衣装のArmatureを探す
            var outfitArmature = FindArmature(outfitRoot);
            if (outfitArmature == null)
            {
                outfitArmature = outfitRoot.transform;
            }

            // プレフィックス/サフィックスを推定
            InferPrefixSuffix(outfitArmature, avatarBoneMap, out var prefix, out var suffix);

            // 全ての子Transform を取得
            var outfitTransforms = outfitArmature.GetComponentsInChildren<Transform>(true);
            var matched = new HashSet<Transform>();

            foreach (var outfitBone in outfitTransforms)
            {
                if (outfitBone == outfitRoot.transform)
                    continue;

                var match = TryMatchBone(outfitBone, avatarBoneMap, prefix, suffix, 
                    avatarHasUpperChest, matched);

                if (match.HasValue)
                {
                    results.Add(match.Value);
                    if (match.Value.AvatarBone != null)
                    {
                        matched.Add(match.Value.AvatarBone);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 衣装からArmatureを探す
        /// </summary>
        private static Transform FindArmature(GameObject root)
        {
            // 一般的なArmature名を検索
            var armatureNames = new[] { "Armature", "armature", "Skeleton", "skeleton", "Root", "root" };

            foreach (var name in armatureNames)
            {
                var armature = root.transform.Find(name);
                if (armature != null)
                    return armature;
            }

            // SkinnedMeshRendererのルートボーンを探す
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.rootBone != null)
            {
                // ルートボーンの親がArmatureの可能性が高い
                var parent = smr.rootBone.parent;
                while (parent != null && parent != root.transform)
                {
                    var lowerName = parent.name.ToLowerInvariant();
                    if (lowerName.Contains("armature") || lowerName.Contains("skeleton"))
                        return parent;
                    parent = parent.parent;
                }

                // 見つからなければルートボーンの親を返す
                if (smr.rootBone.parent != null && smr.rootBone.parent != root.transform)
                    return smr.rootBone.parent;
            }

            return null;
        }

        /// <summary>
        /// プレフィックスとサフィックスを推定する
        /// </summary>
        private static void InferPrefixSuffix(
            Transform outfitArmature, 
            Dictionary<HumanBodyBones, Transform> avatarBoneMap,
            out string prefix, 
            out string suffix)
        {
            prefix = string.Empty;
            suffix = string.Empty;

            if (outfitArmature == null || avatarBoneMap.Count == 0)
                return;

            // Hipsを基準に推定を試みる
            if (!avatarBoneMap.TryGetValue(HumanBodyBones.Hips, out var avatarHips))
                return;

            var avatarHipsName = avatarHips.name;

            // 衣装側でHipsに相当するボーンを探す
            foreach (Transform child in outfitArmature)
            {
                foreach (var pattern in HumanoidBonePatterns.BoneNamePatterns[0]) // Hipsパターン
                {
                    if (BoneNameMatcher.TryInferPrefixSuffix(child.name, pattern, 
                        out var p, out var s))
                    {
                        // このプレフィックス/サフィックスで他のボーンもマッチするか確認
                        int matchCount = CountMatches(outfitArmature, avatarBoneMap, p, s);
                        if (matchCount > 1)
                        {
                            prefix = p;
                            suffix = s;
                            return;
                        }
                    }
                }

                // アバターのHips名との直接比較も試す
                if (BoneNameMatcher.TryInferPrefixSuffix(child.name, avatarHipsName, 
                    out var p2, out var s2))
                {
                    int matchCount = CountMatches(outfitArmature, avatarBoneMap, p2, s2);
                    if (matchCount > 1)
                    {
                        prefix = p2;
                        suffix = s2;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 指定したプレフィックス/サフィックスでマッチするボーン数をカウント
        /// </summary>
        private static int CountMatches(
            Transform outfitArmature,
            Dictionary<HumanBodyBones, Transform> avatarBoneMap,
            string prefix, string suffix)
        {
            int count = 0;
            var outfitTransforms = outfitArmature.GetComponentsInChildren<Transform>(true);

            foreach (var outfitBone in outfitTransforms)
            {
                var baseName = BoneNameMatcher.StripPrefixSuffix(outfitBone.name, prefix, suffix);
                if (string.IsNullOrEmpty(baseName))
                    continue;

                // アバターのボーン名と直接比較
                foreach (var kvp in avatarBoneMap)
                {
                    if (string.Equals(baseName, kvp.Value.name, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                        break;
                    }
                }

                // またはヒューマノイドボーンパターンでマッチ
                if (BoneNameMatcher.IsRecognizedBoneName(baseName))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 単一のボーンをマッチングする
        /// </summary>
        private static MatchResult? TryMatchBone(
            Transform outfitBone,
            Dictionary<HumanBodyBones, Transform> avatarBoneMap,
            string prefix, string suffix,
            bool avatarHasUpperChest,
            HashSet<Transform> alreadyMatched)
        {
            var boneName = outfitBone.name;

            // プレフィックス/サフィックスを除去
            var baseName = BoneNameMatcher.StripPrefixSuffix(boneName, prefix, suffix);
            if (string.IsNullOrEmpty(baseName))
                return null;

            // 1. アバターのボーン名と直接マッチ
            foreach (var kvp in avatarBoneMap)
            {
                if (alreadyMatched.Contains(kvp.Value))
                    continue;

                if (string.Equals(baseName, kvp.Value.name, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchResult
                    {
                        OutfitBone = outfitBone,
                        AvatarBone = kvp.Value,
                        HumanBone = kvp.Key,
                        Confidence = 1.0f
                    };
                }
            }

            // 2. ヒューマノイドボーンパターンでマッチ
            var humanBone = BoneNameMatcher.TryMatchBone(baseName);
            if (humanBone.HasValue)
            {
                // UpperChestの特別処理
                if (humanBone.Value == HumanBodyBones.UpperChest && !avatarHasUpperChest)
                {
                    // アバターにUpperChestがない場合、スキップ（子ボーンは引き続き処理）
                    return new MatchResult
                    {
                        OutfitBone = outfitBone,
                        AvatarBone = null, // マッチなし、でもリストには含める
                        HumanBone = humanBone.Value,
                        Confidence = 0.5f
                    };
                }

                if (avatarBoneMap.TryGetValue(humanBone.Value, out var avatarBone) &&
                    !alreadyMatched.Contains(avatarBone))
                {
                    return new MatchResult
                    {
                        OutfitBone = outfitBone,
                        AvatarBone = avatarBone,
                        HumanBone = humanBone.Value,
                        Confidence = 0.9f
                    };
                }
            }

            // 3. 複数候補がある場合はベストマッチを探す
            var possibleBones = BoneNameMatcher.GetPossibleBones(baseName);
            foreach (var bone in possibleBones)
            {
                if (avatarBoneMap.TryGetValue(bone, out var avatarBone) &&
                    !alreadyMatched.Contains(avatarBone))
                {
                    return new MatchResult
                    {
                        OutfitBone = outfitBone,
                        AvatarBone = avatarBone,
                        HumanBone = bone,
                        Confidence = 0.7f
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 衣装がUpperChestを持っているかどうかをヒューリスティックに判定
        /// </summary>
        public static bool OutfitHasUpperChest(GameObject outfitRoot)
        {
            if (outfitRoot == null)
                return false;

            var transforms = outfitRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                var bone = BoneNameMatcher.TryMatchBone(t.name);
                if (bone == HumanBodyBones.UpperChest)
                    return true;
            }

            return false;
        }
    }
}
