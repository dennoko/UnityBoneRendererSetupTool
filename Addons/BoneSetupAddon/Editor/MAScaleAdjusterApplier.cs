using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Hays.BoneRendererSetup.Matching;

namespace Hays.BoneRendererSetup.Addons
{
    internal static class MAScaleAdjusterApplier
    {
        private const float CollinearAngleThreshold = 30f;
        private const float ScaleChangeThreshold = 0.001f;
        private const float MinDistance = 1e-5f;
        // ボーン長に対してこの割合未満の軸成分はゼロとみなしてスケール1を維持する
        private const float AxisSignificanceRatio = 0.05f;

        private static System.Type _maType;
        private static bool _typeCacheAttempted;

        private static System.Type GetMAType()
        {
            if (_typeCacheAttempted) return _maType;
            _typeCacheAttempted = true;
            _maType = TypeCache.GetTypesDerivedFrom(typeof(Component))
                .FirstOrDefault(t => t.FullName == "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster");
            return _maType;
        }

        public static void TryApplyScaleAdjuster(
            Transform outfitBone,
            Transform avatarBone,
            List<OutfitBoneMapper.MatchResult> matches)
        {
            BoneRotationAligner.FindMatchedChild(
                outfitBone, matches, out var outfitChild, out var avatarChild);
            if (outfitChild == null || avatarChild == null) return;

            var outfitVec = outfitChild.position - outfitBone.position;
            var avatarVec = avatarChild.position - avatarBone.position;

            if (outfitVec.magnitude < MinDistance || avatarVec.magnitude < MinDistance) return;

            // 直線上にない子ボーン（分岐ボーン）は無視
            if (Vector3.Angle(outfitVec, avatarVec) >= CollinearAngleThreshold) return;

            // 衣装ボーンのローカル空間で「現在の子の位置」と「目標（アバターの子）の位置」を取得し、
            // 軸ごとにスケールを計算する。
            // アバター側も衣装ボーンの空間へ変換するのが重要: 回転合わせで 3 軸は平行になっているが、
            // 軸の並び（どの軸が骨方向か）は入れ替わりうるため、アバターボーンの空間で成分を取ると
            // 別の軸の成分同士を比較してしまう。同じ空間へ揃えることで規約差もスケール差も吸収する。
            Vector3 outfitLocal = outfitBone.InverseTransformPoint(outfitChild.position);
            Vector3 avatarLocal = outfitBone.InverseTransformPoint(avatarChild.position);
            Vector3 scale = ComputePerAxisScale(outfitLocal, avatarLocal);

            var maType = GetMAType();
            if (maType == null) return;

            if (Vector3.Distance(scale, Vector3.one) < ScaleChangeThreshold)
            {
                // スケール調整が不要でも、過去に付与された Adjuster が古い値を持っている場合が
                // あるため、放置せず 1 にリセットする（左右同期で古い値が伝播するのを防ぐ）
                ResetScaleAdjusterIfPresent(outfitBone, maType);
                return;
            }

            SetOrUpdateScaleAdjuster(outfitBone, maType, scale);
            Debug.Log($"[BoneRenderer] Applied MA Scale Adjuster to '{outfitBone.name}': scale = {scale}");
        }

        /// <summary>
        /// 各軸の比率を個別に計算する。
        /// ボーン長に対して成分が小さい軸（ほぼゼロ）はスケール1を維持する。
        /// 符号が反転するケース（回転が大きくずれている）はスケール1にフォールバックする。
        /// </summary>
        private static Vector3 ComputePerAxisScale(Vector3 outfitLocal, Vector3 avatarLocal)
        {
            float boneMag = outfitLocal.magnitude;
            return new Vector3(
                ComputeAxisScale(outfitLocal.x, avatarLocal.x, boneMag),
                ComputeAxisScale(outfitLocal.y, avatarLocal.y, boneMag),
                ComputeAxisScale(outfitLocal.z, avatarLocal.z, boneMag)
            );
        }

        private static float ComputeAxisScale(float outfitComp, float avatarComp, float boneMag)
        {
            if (Mathf.Abs(outfitComp) < boneMag * AxisSignificanceRatio) return 1f;
            float s = avatarComp / outfitComp;
            // 符号反転は回転ずれを意味するので対象外
            return s > 0f ? s : 1f;
        }

        private static void ResetScaleAdjusterIfPresent(Transform outfitBone, System.Type maType)
        {
            var component = outfitBone.GetComponent(maType);
            if (component == null) return;

            var so = new SerializedObject(component);
            var scaleProp = so.FindProperty("m_Scale");
            if (scaleProp == null || scaleProp.vector3Value == Vector3.one) return;

            Undo.RecordObject(component, "Reset MA Scale Adjuster");
            so.Update();
            scaleProp.vector3Value = Vector3.one;
            so.ApplyModifiedProperties();
            Debug.Log($"[BoneRenderer] Reset MA Scale Adjuster on '{outfitBone.name}' (scale not needed)");
        }

        private static void SetOrUpdateScaleAdjuster(Transform outfitBone, System.Type maType, Vector3 scale)
        {
            var component = outfitBone.GetComponent(maType);
            if (component == null)
                component = Undo.AddComponent(outfitBone.gameObject, maType);

            var so = new SerializedObject(component);
            so.Update();

            var scaleProp = so.FindProperty("m_Scale");
            if (scaleProp == null) return;

            Undo.RecordObject(component, "Apply MA Scale Adjuster");
            scaleProp.vector3Value = scale;
            so.ApplyModifiedProperties();
        }

    }
}
