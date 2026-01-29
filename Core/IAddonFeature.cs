using UnityEngine;
using UnityEditor;

namespace Hays.BoneRendererSetup.Core
{
    /// <summary>
    /// Addon機能のインターフェース
    /// </summary>
    public interface IAddonFeature
    {
        string DisplayName { get; }
        
        /// <summary>
        /// ツールウィンドウ内での描画
        /// </summary>
        void OnGUI(GameObject avatar, GameObject outfit);
        
        /// <summary>
        /// シーンビューでの描画・操作
        /// </summary>
        void OnSceneGUI(SceneView sceneView);
        
        void OnEnable();
        void OnDisable();
    }
}
