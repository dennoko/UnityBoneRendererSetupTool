using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Hays.BoneRendererSetup.Core
{
    /// <summary>
    /// BoneRendererコンポーネントの操作を行うサービスクラス
    /// </summary>
    public static class BoneRendererService
    {
        /// <summary>
        /// BoneRendererをセットアップする
        /// </summary>
        /// <param name="target">対象のGameObject</param>
        /// <param name="bones">設定するボーンのTransform配列</param>
        /// <param name="color">BoneRendererの色</param>
        /// <returns>成功した場合true</returns>
        public static bool SetupBoneRenderer(GameObject target, List<Transform> bones, Color color)
        {
            if (target == null || bones == null || bones.Count == 0)
            {
                Debug.LogWarning("[BoneRendererSetup] Invalid target or empty bone list.");
                return false;
            }

            // BoneRendererを取得または追加
            var boneRenderer = target.GetComponent<BoneRenderer>();
            if (boneRenderer == null)
            {
                boneRenderer = Undo.AddComponent<BoneRenderer>(target);
            }

            // ボーンを設定
            if (!AssignTransforms(boneRenderer, bones, out var error))
            {
                Debug.LogError($"[BoneRendererSetup] Failed to assign bones: {error}");
                return false;
            }

            // 色設定
            SetColor(boneRenderer, color);

            EditorUtility.SetDirty(boneRenderer);
            return true;
        }

        /// <summary>
        /// BoneRendererを削除する
        /// </summary>
        public static bool RemoveBoneRenderer(GameObject target)
        {
            if (target == null)
                return false;

            var renderers = target.GetComponents<BoneRenderer>();
            if (renderers.Length == 0)
                return false;

            foreach (var renderer in renderers)
            {
                Undo.DestroyObjectImmediate(renderer);
            }

            return true;
        }

        /// <summary>
        /// BoneRendererが存在するかどうか
        /// </summary>
        public static bool HasBoneRenderer(GameObject target)
        {
            return target != null && target.GetComponent<BoneRenderer>() != null;
        }

        /// <summary>
        /// 色を設定する
        /// </summary>
        public static bool SetColor(BoneRenderer boneRenderer, Color color)
        {
            if (boneRenderer == null)
                return false;

            var so = new SerializedObject(boneRenderer);
            var colorProp = FindColorProperty(so);

            if (colorProp == null)
                return false;

            Undo.RecordObject(boneRenderer, "Set Bone Renderer Color");
            so.Update();
            colorProp.colorValue = color;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(boneRenderer);
            return true;
        }

        private static bool AssignTransforms(BoneRenderer boneRenderer, List<Transform> transforms, out string error)
        {
            var so = new SerializedObject(boneRenderer);
            var transformsProp = so.FindProperty("m_Transforms") ?? so.FindProperty("m_Bones");

            if (transformsProp == null || !transformsProp.isArray)
            {
                error = "BoneRenderer does not expose a transforms array property.";
                return false;
            }

            Undo.RecordObject(boneRenderer, "Setup Bone Renderer");

            so.Update();
            transformsProp.arraySize = transforms.Count;
            for (int i = 0; i < transforms.Count; i++)
            {
                transformsProp.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
            }
            so.ApplyModifiedProperties();

            error = null;
            return true;
        }

        private static SerializedProperty FindColorProperty(SerializedObject so)
        {
            var candidates = new[] { "m_Color", "m_BoneColor", "color", "boneColor" };
            foreach (var name in candidates)
            {
                var prop = so.FindProperty(name);
                if (prop != null && prop.propertyType == SerializedPropertyType.Color)
                    return prop;
            }
            return null;
        }
    }
}
