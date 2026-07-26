using System.Collections.Generic;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hays.BoneRendererSetup.UI
{
    public partial class BoneRendererSetupWindow : EditorWindow
    {
        // UI/*.uxml, UI/*.uss の .meta に記載された GUID
        private const string UxmlGuid = "81929f1bb3567eb340e4d1d4754e7a10";
        private const string ThemeUssGuid = "ffa252124638f37251ea5c7b74c4239c";
        private const string WindowUssGuid = "f9775a3dbd2c12b575b60341fd2c578d";

        // ─── State ───────────────────────────────────────────────────────────

        private GameObject _avatar;
        private GameObject _outfit;
        private List<IAddonFeature> _addons = new List<IAddonFeature>();
        private readonly List<IMGUIContainer> _addonContainers = new List<IMGUIContainer>();

        public static BoneRendererSetupWindow Instance { get; private set; }
        public GameObject CurrentAvatar => _avatar;
        public GameObject CurrentOutfit => _outfit;

        // ─── Status ──────────────────────────────────────────────────────────

        public enum StatusType { Info, Success, Error, Warning }

        private Label _statusLabel;
        private IVisualElementScheduledItem _statusResetSchedule;

        // ─── UI 要素 ─────────────────────────────────────────────────────────

        private ObjectField _avatarField;
        private ObjectField _outfitField;
        private Label _avatarInfoLabel;
        private Label _outfitInfoLabel;
        private Label _outfitWarningLabel;
        private Button _avatarSetupButton;
        private Button _avatarRemoveButton;
        private Button _outfitSetupButton;
        private Button _outfitRemoveButton;

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
            Selection.selectionChanged += OnSelectionChangedForAddons;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChangedForAddons;
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

        /// <summary>アドオンパネルは IMGUI 併用のため、選択変更時に明示的に再描画する。</summary>
        private void OnSelectionChangedForAddons()
        {
            foreach (var container in _addonContainers)
                container.MarkDirtyRepaint();
        }

        // ─── CreateGUI (UI Toolkit エントリポイント) ─────────────────────────

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // テーマ非依存のためのルートクラスを適用
            root.AddToClassList("dennoko-root");
            // USS ロード失敗時も背景が明るくならないよう Surface0 を C# 側でも保証
            root.style.backgroundColor = (Color)new Color32(0x12, 0x12, 0x12, 0xFF);
            root.style.flexGrow = 1;

            // 標準フォント: OS のメイリオを全体に適用（全テキスト要素へ継承される）。
            // 生成・アトラス保護・キャッシュ消失時の再適用はすべて DennokoUIFont が行う。
            // ⚠ ここで FontAsset を直接生成しないこと（DennokoUIFont.cs のコメント参照）。
            DennokoUIFont.Apply(root);

            LoadStyleSheet(root, ThemeUssGuid);
            LoadStyleSheet(root, WindowUssGuid);

            string uxmlPath = AssetDatabase.GUIDToAssetPath(UxmlGuid);
            var uxml = string.IsNullOrEmpty(uxmlPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
            {
                root.Add(new Label("UXML Asset が見つかりません。GUID を確認してください。"));
                return;
            }
            uxml.CloneTree(root);

            InitializeUI(root);
        }

        private static void LoadStyleSheet(VisualElement root, string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var uss = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (uss != null)
                root.styleSheets.Add(uss);
            else
                Debug.LogWarning($"[BoneRendererSetupWindow] USS が見つかりません。GUID を確認してください: {guid}");
        }

        // ─── バインディング ─────────────────────────────────────────────────

        private void InitializeUI(VisualElement root)
        {
            _statusLabel = root.Q<Label>("status-label");

            var installGuideRoot = root.Q<VisualElement>("install-guide-root");
            var mainScroll = root.Q<ScrollView>("main-scroll");

#if HAS_ANIMATION_RIGGING
            installGuideRoot.style.display = DisplayStyle.None;
            mainScroll.style.display = DisplayStyle.Flex;
            InitializeMainUI(root);
#else
            installGuideRoot.style.display = DisplayStyle.Flex;
            mainScroll.style.display = DisplayStyle.None;
            InitializeInstallGuide(root);
#endif
        }

#if HAS_ANIMATION_RIGGING
        private void InitializeMainUI(VisualElement root)
        {
            root.Q<Button>("search-avatar-button").clicked += SearchAvatarInScene;

            _avatarField = root.Q<ObjectField>("avatar-field");
            _avatarField.objectType = typeof(GameObject);
            _avatarField.SetValueWithoutNotify(_avatar);
            _avatarField.RegisterValueChangedCallback(OnAvatarFieldChanged);
            root.Q<Button>("avatar-clear-button").clicked += ClearAvatar;
            _avatarInfoLabel = root.Q<Label>("avatar-info-label");
            _avatarSetupButton = root.Q<Button>("avatar-setup-button");
            _avatarSetupButton.clicked += () => { SetupAvatar(); RefreshAvatarUI(); };
            _avatarRemoveButton = root.Q<Button>("avatar-remove-button");
            _avatarRemoveButton.clicked += () => { RemoveRenderer(_avatar); RefreshAvatarUI(); };

            _outfitField = root.Q<ObjectField>("outfit-field");
            _outfitField.objectType = typeof(GameObject);
            _outfitField.SetValueWithoutNotify(_outfit);
            _outfitField.RegisterValueChangedCallback(OnOutfitFieldChanged);
            root.Q<Button>("outfit-clear-button").clicked += ClearOutfit;
            _outfitInfoLabel = root.Q<Label>("outfit-info-label");
            _outfitWarningLabel = root.Q<Label>("outfit-warning-label");
            _outfitSetupButton = root.Q<Button>("outfit-setup-button");
            _outfitSetupButton.clicked += () => { SetupOutfit(); RefreshOutfitUI(); };
            _outfitRemoveButton = root.Q<Button>("outfit-remove-button");
            _outfitRemoveButton.clicked += () => { RemoveRenderer(_outfit); RefreshOutfitUI(); };

            BuildAddonCards(root.Q<VisualElement>("addon-container"));

            var avatarColorField = root.Q<ColorField>("avatar-color-field");
            avatarColorField.value = BoneRendererSettings.AvatarColor;
            avatarColorField.RegisterValueChangedCallback(evt => BoneRendererSettings.AvatarColor = evt.newValue);

            var outfitColorField = root.Q<ColorField>("outfit-color-field");
            outfitColorField.value = BoneRendererSettings.OutfitColor;
            outfitColorField.RegisterValueChangedCallback(evt => BoneRendererSettings.OutfitColor = evt.newValue);

            RefreshAvatarUI();
            RefreshOutfitUI();
        }

        private void OnAvatarFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            var newAvatar = evt.newValue as GameObject;
            if (_avatar != null && newAvatar != _avatar)
                RemoveRenderer(_avatar);
            _avatar = newAvatar;
            if (_avatar != null && CanSetupAvatar())
                SetupAvatar();
            RefreshAvatarUI();
            RefreshOutfitUI();
        }

        private void OnOutfitFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            var newOutfit = evt.newValue as GameObject;
            if (_outfit != null && newOutfit != _outfit)
                RemoveRenderer(_outfit);
            _outfit = newOutfit;
            if (_outfit != null && CanSetupOutfit())
                SetupOutfit();
            RefreshOutfitUI();
        }

        private void ClearAvatar()
        {
            if (_avatar != null) RemoveRenderer(_avatar);
            _avatar = null;
            _avatarField.SetValueWithoutNotify(null);
            RefreshAvatarUI();
            RefreshOutfitUI();
        }

        private void ClearOutfit()
        {
            if (_outfit != null) RemoveRenderer(_outfit);
            _outfit = null;
            _outfitField.SetValueWithoutNotify(null);
            RefreshOutfitUI();
        }
#endif

        // RefreshAvatarUI / RefreshOutfitUI は Actions.cs (SetAvatarAndSetup) からも
        // 呼ばれるため、HAS_ANIMATION_RIGGING の有無に関わらず常にコンパイルされる必要がある。
        // 対象の UI 要素は #if HAS_ANIMATION_RIGGING 側で生成されなかった場合 null のままなので、
        // 各メソッド冒頭の null チェックで安全に no-op になる。

        /// <summary>AVATAR カードの情報表示・ボタン活性状態を現在の _avatar に合わせて更新する。</summary>
        private void RefreshAvatarUI()
        {
            if (_avatarInfoLabel == null) return;

            if (_avatar != null)
            {
                if (AvatarBoneMapper.IsHumanoidAvatar(_avatar))
                {
                    var boneCount = AvatarBoneMapper.GetHumanoidBones(_avatar).Count;
                    var hasUpperChest = AvatarBoneMapper.HasUpperChest(_avatar);
                    _avatarInfoLabel.text = $"Humanoid Avatar ({boneCount} bones)  |  UpperChest: {(hasUpperChest ? "あり" : "なし")}";
                    _avatarInfoLabel.EnableInClassList("dennoko-status--warning", false);
                }
                else
                {
                    _avatarInfoLabel.text = "Humanoid Avatar ではありません";
                    _avatarInfoLabel.EnableInClassList("dennoko-status--warning", true);
                }
                _avatarInfoLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _avatarInfoLabel.style.display = DisplayStyle.None;
            }

            _avatarSetupButton.SetEnabled(CanSetupAvatar());
            _avatarRemoveButton.SetEnabled(HasBoneRenderer(_avatar));
        }

        /// <summary>OUTFIT カードの情報表示・ボタン活性状態を現在の _outfit / _avatar に合わせて更新する。</summary>
        private void RefreshOutfitUI()
        {
            if (_outfitInfoLabel == null) return;

            if (_outfit != null)
            {
                var smrCount = _outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                var hasArmature = HasArmature(_outfit);
                _outfitInfoLabel.text = $"SkinnedMeshRenderer: {smrCount}  |  Armature: {(hasArmature ? "検出" : "未検出")}";
                _outfitInfoLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _outfitInfoLabel.style.display = DisplayStyle.None;
            }

            _outfitWarningLabel.style.display =
                (_outfit != null && _avatar == null) ? DisplayStyle.Flex : DisplayStyle.None;

            _outfitSetupButton.SetEnabled(CanSetupOutfit());
            _outfitRemoveButton.SetEnabled(HasBoneRenderer(_outfit));
        }

#if HAS_ANIMATION_RIGGING
        // ─── アドオンカード (動的生成) ────────────────────────────────────
        // カードの外枠は UI Toolkit (USS) で装飾し、アドオン自身の中身は
        // 既存の IAddonFeature.OnGUI (IMGUI) をそのまま IMGUIContainer でホストする。

        private void BuildAddonCards(VisualElement container)
        {
            container.Clear();
            _addonContainers.Clear();

            foreach (var addon in _addons)
            {
                var captured = addon;

                var card = new VisualElement();
                card.AddToClassList("dennoko-card");

                var titleLabel = new Label(captured.DisplayName.ToUpper());
                titleLabel.AddToClassList("dennoko-section-title");
                titleLabel.AddToClassList("dennoko-card-header");
                card.Add(titleLabel);

                var imguiContainer = new IMGUIContainer(() =>
                {
                    ApplyAddonLightThemeFix();
                    try
                    {
                        captured.OnGUI(_avatar, _outfit);
                    }
                    finally
                    {
                        RestoreAddonLightThemeFix();
                    }
                });
                card.Add(imguiContainer);
                _addonContainers.Add(imguiContainer);

                container.Add(card);
            }
        }

        // ─── アドオン (IMGUI) 用の Light テーマ文字色フィックス ───────────
        // IAddonFeature.OnGUI は素の EditorGUILayout / EditorStyles を使うため、
        // テーマ USS の対象外になる。Personal Light テーマだと暗いカード上に
        // 黒文字が乗って読めなくなるため、描画直前だけ文字色を差し替える。

        private static bool _addonThemeOverrideActive;
        private static Color _addonBackupLabel, _addonBackupBoldLabel, _addonBackupToggle, _addonBackupOnToggle, _addonBackupHelpBox;

        private static void ApplyAddonLightThemeFix()
        {
            if (EditorGUIUtility.isProSkin) return;
            _addonThemeOverrideActive = true;

            _addonBackupLabel = EditorStyles.label.normal.textColor;
            _addonBackupBoldLabel = EditorStyles.boldLabel.normal.textColor;
            _addonBackupToggle = EditorStyles.toggle.normal.textColor;
            _addonBackupOnToggle = EditorStyles.toggle.onNormal.textColor;
            _addonBackupHelpBox = EditorStyles.helpBox.normal.textColor;

            var textColor = new Color(0.8f, 0.8f, 0.8f); // --dennoko-text-secondary 相当
            EditorStyles.label.normal.textColor = textColor;
            EditorStyles.boldLabel.normal.textColor = textColor;
            EditorStyles.toggle.normal.textColor = textColor;
            EditorStyles.toggle.onNormal.textColor = textColor;
            EditorStyles.helpBox.normal.textColor = textColor;
        }

        private static void RestoreAddonLightThemeFix()
        {
            if (!_addonThemeOverrideActive) return;
            _addonThemeOverrideActive = false;

            EditorStyles.label.normal.textColor = _addonBackupLabel;
            EditorStyles.boldLabel.normal.textColor = _addonBackupBoldLabel;
            EditorStyles.toggle.normal.textColor = _addonBackupToggle;
            EditorStyles.toggle.onNormal.textColor = _addonBackupOnToggle;
            EditorStyles.helpBox.normal.textColor = _addonBackupHelpBox;
        }
#else
        private void InitializeInstallGuide(VisualElement root)
        {
            root.Q<Button>("open-package-manager-button").clicked +=
                () => UnityEditor.PackageManager.UI.Window.Open("com.unity.animation.rigging");
        }
#endif

        // ─── ステータスバー ─────────────────────────────────────────────────

        /// <summary>ステータスを表示する。Info 以外は autoResetSeconds 後に Ready へ自動復帰。</summary>
        public void SetStatus(string message, StatusType type, double autoResetSeconds = 4.0)
        {
            if (_statusLabel == null) return; // UXML ロード失敗時・要素名変更時の NRE 防止

            _statusLabel.text = message;
            _statusLabel.EnableInClassList("dennoko-status--success", type == StatusType.Success);
            _statusLabel.EnableInClassList("dennoko-status--error", type == StatusType.Error);
            _statusLabel.EnableInClassList("dennoko-status--warning", type == StatusType.Warning);

            _statusResetSchedule?.Pause();
            if (type != StatusType.Info)
            {
                _statusResetSchedule = _statusLabel.schedule
                    .Execute(() => SetStatus("Ready", StatusType.Info))
                    .StartingIn((long)(autoResetSeconds * 1000));
            }
        }
    }
}
