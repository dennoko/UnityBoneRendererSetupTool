using System.Collections.Generic;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hays.BoneRendererSetup.UI
{
    public partial class BoneRendererSetupWindow : EditorWindow
    {
        // ─── State ───────────────────────────────────────────────────────────

        private GameObject _avatar;
        private GameObject _outfit;
        private List<IAddonFeature> _addons = new List<IAddonFeature>();

        public static BoneRendererSetupWindow Instance { get; private set; }
        public GameObject CurrentAvatar => _avatar;
        public GameObject CurrentOutfit => _outfit;

        private Vector2 _scrollPosition;

        // ─── Status ──────────────────────────────────────────────────────────

        public enum StatusType { Info, Success, Error, Warning }
        private string     _statusMessage   = "Ready";
        private StatusType _statusType      = StatusType.Info;
        private double     _statusResetTime = -1.0;

        // ─── Window Registration ─────────────────────────────────────────────

        [MenuItem("dennokoworks/BoneRendererSetupTool")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoneRendererSetupWindow>("Bone Renderer Setup");
            window.minSize = new Vector2(320, 400);
            window.Show();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            Instance = this;
            LoadAddons();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            foreach (var addon in _addons)
                addon.OnDisable();
            if (Instance == this) Instance = null;
        }

        private void LoadAddons()
        {
            _addons.Clear();
            var types = TypeCache.GetTypesDerivedFrom<IAddonFeature>();
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                try
                {
                    var addon = (IAddonFeature)System.Activator.CreateInstance(type);
                    addon.OnEnable();
                    _addons.Add(addon);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BoneRendererSetup] Failed to load addon {type.Name}: {e.Message}");
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            foreach (var addon in _addons)
                addon.OnSceneGUI(sceneView);
        }

        // ─── OnGUI Entry Point ───────────────────────────────────────────────

        private void OnGUI()
        {
            if (_statusResetTime > 0 && EditorApplication.timeSinceStartup > _statusResetTime)
            {
                _statusMessage   = "Ready";
                _statusType      = StatusType.Info;
                _statusResetTime = -1.0;
            }

            BoneRendererTheme.Initialize();
            BoneRendererTheme.PushEditorTheme();
            try
            {
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BoneRendererTheme.Surface0);

#if !HAS_ANIMATION_RIGGING
                DrawInstallGuide();
#else
                DrawHeader();
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                DrawSettingsArea();
                EditorGUILayout.EndScrollView();
                DrawStatusBar();
#endif
            }
            finally
            {
                BoneRendererTheme.PopEditorTheme();
            }
        }

        // ─── Header ──────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Bone Renderer Setup", BoneRendererTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            DrawSeparator();
        }

        // ─── Settings Area ───────────────────────────────────────────────────

        private void DrawSettingsArea()
        {
            GUILayout.BeginVertical();
            GUILayout.Space(4);

            DrawSection("UTILITY", DrawUtilityContent);
            DrawSection("AVATAR", DrawAvatarContent);
            DrawSection("OUTFIT", DrawOutfitContent);

            foreach (var addon in _addons)
            {
                var captured = addon;
                DrawSection(captured.DisplayName.ToUpper(), () => captured.OnGUI(_avatar, _outfit));
            }

            DrawSection("COLOR SETTINGS", DrawColorSettings);

            GUILayout.Space(4);
            GUILayout.EndVertical();
        }

        // ─── Section Contents ────────────────────────────────────────────────

        private void DrawColorSettings()
        {
            EditorGUI.BeginChangeCheck();
            var avatarColor = EditorGUILayout.ColorField("Avatar Color", BoneRendererSettings.AvatarColor);
            if (EditorGUI.EndChangeCheck())
                BoneRendererSettings.AvatarColor = avatarColor;

            EditorGUI.BeginChangeCheck();
            var outfitColor = EditorGUILayout.ColorField("Outfit Color", BoneRendererSettings.OutfitColor);
            if (EditorGUI.EndChangeCheck())
                BoneRendererSettings.OutfitColor = outfitColor;
        }

        private void DrawAvatarContent()
        {
            // ObjectField + Clear
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newAvatar = (GameObject)EditorGUILayout.ObjectField(_avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (_avatar != null && newAvatar != _avatar)
                    RemoveRenderer(_avatar);
                _avatar = newAvatar;
                if (_avatar != null && CanSetupAvatar())
                    SetupAvatar();
            }
            if (GUILayout.Button("Clear", BoneRendererTheme.MiniButtonStyle, GUILayout.Width(50)))
            {
                if (_avatar != null) RemoveRenderer(_avatar);
                _avatar = null;
            }
            GUILayout.EndHorizontal();

            // Info / Warning
            if (_avatar != null)
            {
                if (AvatarBoneMapper.IsHumanoidAvatar(_avatar))
                {
                    var boneCount     = AvatarBoneMapper.GetHumanoidBones(_avatar).Count;
                    var hasUpperChest = AvatarBoneMapper.HasUpperChest(_avatar);
                    GUILayout.Box(
                        $"Humanoid Avatar ({boneCount} bones)  |  UpperChest: {(hasUpperChest ? "あり" : "なし")}",
                        BoneRendererTheme.StatusInfoStyle, GUILayout.ExpandWidth(true));
                }
                else
                {
                    GUILayout.Box("Humanoid Avatar ではありません",
                        BoneRendererTheme.StatusWarningStyle, GUILayout.ExpandWidth(true));
                }
            }

            // Action buttons
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(!CanSetupAvatar()))
            {
                if (GUILayout.Button("Setup", BoneRendererTheme.ActionButtonStyle))
                    SetupAvatar();
            }
            using (new EditorGUI.DisabledGroupScope(!HasBoneRenderer(_avatar)))
            {
                if (GUILayout.Button("Remove", BoneRendererTheme.ActionButtonStyle))
                    RemoveRenderer(_avatar);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawOutfitContent()
        {
            // ObjectField + Clear
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newOutfit = (GameObject)EditorGUILayout.ObjectField(_outfit, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (_outfit != null && newOutfit != _outfit)
                    RemoveRenderer(_outfit);
                _outfit = newOutfit;
                if (_outfit != null && CanSetupOutfit())
                    SetupOutfit();
            }
            if (GUILayout.Button("Clear", BoneRendererTheme.MiniButtonStyle, GUILayout.Width(50)))
            {
                if (_outfit != null) RemoveRenderer(_outfit);
                _outfit = null;
            }
            GUILayout.EndHorizontal();

            // Info / Warning
            if (_outfit != null)
            {
                var smrCount   = _outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                var hasArmature = HasArmature(_outfit);
                GUILayout.Box(
                    $"SkinnedMeshRenderer: {smrCount}  |  Armature: {(hasArmature ? "検出" : "未検出")}",
                    BoneRendererTheme.StatusInfoStyle, GUILayout.ExpandWidth(true));
            }

            if (_outfit != null && _avatar == null)
            {
                GUILayout.Box("セットアップにはアバターの参照が必要です",
                    BoneRendererTheme.StatusWarningStyle, GUILayout.ExpandWidth(true));
            }

            // Action buttons
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(!CanSetupOutfit()))
            {
                if (GUILayout.Button("Setup", BoneRendererTheme.ActionButtonStyle))
                    SetupOutfit();
            }
            using (new EditorGUI.DisabledGroupScope(!HasBoneRenderer(_outfit)))
            {
                if (GUILayout.Button("Remove", BoneRendererTheme.ActionButtonStyle))
                    RemoveRenderer(_outfit);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawUtilityContent()
        {
            if (GUILayout.Button("シーンからアバターを検索", BoneRendererTheme.SecondaryButtonStyle))
                SearchAvatarInScene();
        }

        // ─── Status Bar ──────────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            GUILayout.Box(_statusMessage, GetStatusStyle(_statusType), GUILayout.ExpandWidth(true));
        }

        private GUIStyle GetStatusStyle(StatusType type) => type switch
        {
            StatusType.Success => BoneRendererTheme.StatusSuccessStyle,
            StatusType.Error   => BoneRendererTheme.StatusErrorStyle,
            StatusType.Warning => BoneRendererTheme.StatusWarningStyle,
            _                  => BoneRendererTheme.StatusInfoStyle,
        };

        // ─── Layout Helpers ──────────────────────────────────────────────────

        private void DrawSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(BoneRendererTheme.CardStyle);
            GUILayout.Label(title, BoneRendererTheme.SectionHeaderStyle);
            DrawSeparator();
            content?.Invoke();
            GUILayout.EndVertical();
        }

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, BoneRendererTheme.Outline);
            EditorGUILayout.Space(4);
        }

        // ─── Status Helper ───────────────────────────────────────────────────

        public void SetStatus(string message, StatusType type, double autoResetSeconds = 4.0)
        {
            _statusMessage   = message;
            _statusType      = type;
            _statusResetTime = type == StatusType.Info
                ? -1.0
                : EditorApplication.timeSinceStartup + autoResetSeconds;
            Repaint();
        }

        // ─── Install Guide (no Animation Rigging) ────────────────────────────

#if !HAS_ANIMATION_RIGGING
        private void DrawInstallGuide()
        {
            EditorGUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Bone Renderer Setup", BoneRendererTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            DrawSeparator();

            DrawSection("SETUP REQUIRED", () =>
            {
                GUILayout.Box(
                    "このツールを使用するには Unity 公式の [Animation Rigging] パッケージが必要です。\n" +
                    "現在はインストールされていないため、機能が無効化されています。",
                    BoneRendererTheme.StatusErrorStyle, GUILayout.ExpandWidth(true));
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Package Manager を開く", BoneRendererTheme.ActionButtonStyle))
                    UnityEditor.PackageManager.UI.Window.Open("com.unity.animation.rigging");
            });

            DrawSection("インストール手順", () =>
            {
                GUILayout.Box(
                    "1. 上のボタンをクリックして Package Manager を開く\n" +
                    "2. 左上のプルダウンから [Unity Registry] を選択\n" +
                    "3. リストから [Animation Rigging] を探して [Install] をクリック",
                    BoneRendererTheme.StatusInfoStyle, GUILayout.ExpandWidth(true));
            });
        }
#endif
    }
}
