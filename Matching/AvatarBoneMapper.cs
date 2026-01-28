using System.Collections.Generic;
using Hays.BoneRendererSetup.Core;
using UnityEngine;

namespace Hays.BoneRendererSetup.Matching
{
    /// <summary>
    /// アバターのヒューマノイドボーンを取得するマッパー
    /// </summary>
    public static class AvatarBoneMapper
    {
        /// <summary>
        /// アバターのヒューマノイドボーンを取得する
        /// </summary>
        /// <param name="avatarRoot">アバターのルートGameObject</param>
        /// <returns>ヒューマノイドボーンのTransformリスト</returns>
        public static List<Transform> GetHumanoidBones(GameObject avatarRoot)
        {
            if (avatarRoot == null)
                return new List<Transform>();

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return new List<Transform>();

            return CollectHumanoidTransforms(animator);
        }

        /// <summary>
        /// アバターがヒューマノイドかどうかを判定
        /// </summary>
        public static bool IsHumanoidAvatar(GameObject avatarRoot)
        {
            if (avatarRoot == null)
                return false;

            var animator = avatarRoot.GetComponent<Animator>();
            return animator != null && animator.avatar != null && 
                   animator.avatar.isValid && animator.avatar.isHuman;
        }

        /// <summary>
        /// アバターのAnimatorを取得
        /// </summary>
        public static Animator GetAnimator(GameObject avatarRoot)
        {
            if (avatarRoot == null)
                return null;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                return animator;

            return null;
        }

        /// <summary>
        /// アバターがUpperChestを持っているかどうか
        /// </summary>
        public static bool HasUpperChest(GameObject avatarRoot)
        {
            var animator = GetAnimator(avatarRoot);
            if (animator == null)
                return false;

            return animator.GetBoneTransform(HumanBodyBones.UpperChest) != null;
        }

        /// <summary>
        /// 指定したHumanBodyBonesのTransformを取得
        /// </summary>
        public static Transform GetBoneTransform(GameObject avatarRoot, HumanBodyBones bone)
        {
            var animator = GetAnimator(avatarRoot);
            if (animator == null)
                return null;

            return animator.GetBoneTransform(bone);
        }

        /// <summary>
        /// HumanBodyBonesからTransformへのマッピングを取得
        /// </summary>
        public static Dictionary<HumanBodyBones, Transform> GetBoneMapping(GameObject avatarRoot)
        {
            var result = new Dictionary<HumanBodyBones, Transform>();
            var animator = GetAnimator(avatarRoot);

            if (animator == null)
                return result;

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    result[bone] = transform;
                }
            }

            return result;
        }

        /// <summary>
        /// TransformからHumanBodyBonesへの逆引きマッピングを取得
        /// </summary>
        public static Dictionary<Transform, HumanBodyBones> GetReverseBoneMapping(GameObject avatarRoot)
        {
            var result = new Dictionary<Transform, HumanBodyBones>();
            var animator = GetAnimator(avatarRoot);

            if (animator == null)
                return result;

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                var transform = animator.GetBoneTransform(bone);
                if (transform != null && !result.ContainsKey(transform))
                {
                    result[transform] = bone;
                }
            }

            return result;
        }

        private static List<Transform> CollectHumanoidTransforms(Animator animator)
        {
            var list = new List<Transform>(64);
            var seen = new HashSet<Transform>();

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                var t = animator.GetBoneTransform(bone);
                if (t != null && seen.Add(t))
                {
                    list.Add(t);
                }
            }

            return list;
        }
    }
}
