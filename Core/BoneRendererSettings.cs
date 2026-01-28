using UnityEditor;
using UnityEngine;

namespace Hays.BoneRendererSetup.Core
{
    /// <summary>
    /// BoneRendererSetupToolの設定を管理するクラス
    /// </summary>
    public static class BoneRendererSettings
    {
        private const string AvatarColorKey = "Hays.BoneRendererSetup.AvatarColor";
        private const string OutfitColorKey = "Hays.BoneRendererSetup.OutfitColor";

        /// <summary>
        /// アバター用BoneRendererの色（デフォルト: 緑系）
        /// </summary>
        public static Color AvatarColor
        {
            get => GetColor(AvatarColorKey, new Color(0.2f, 1.0f, 0.2f, 1.0f));
            set => SetColor(AvatarColorKey, value);
        }

        /// <summary>
        /// 衣装用BoneRendererの色（デフォルト: オレンジ系）
        /// </summary>
        public static Color OutfitColor
        {
            get => GetColor(OutfitColorKey, new Color(1.0f, 0.6f, 0.0f, 1.0f));
            set => SetColor(OutfitColorKey, value);
        }

        private static Color GetColor(string key, Color defaultColor)
        {
            var str = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(str))
                return defaultColor;
            
            if (ColorUtility.TryParseHtmlString("#" + str, out var color))
                return color;
            
            return defaultColor;
        }

        private static void SetColor(string key, Color color)
        {
            var str = ColorUtility.ToHtmlStringRGBA(color);
            EditorPrefs.SetString(key, str);
        }
    }
}
