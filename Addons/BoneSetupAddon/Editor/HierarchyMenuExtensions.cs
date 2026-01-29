using UnityEditor;
using UnityEngine;
using Hays.BoneRendererSetup.UI;
using Hays.BoneRendererSetup.Matching;
using System.Linq;

namespace Hays.BoneRendererSetup.Addons
{
    public static class AddonContextMenus
    {
        [MenuItem("GameObject/BoneRenderer Setup/Align with Avatar", false, 10)]
        public static void AlignWithAvatar(MenuCommand command)
        {
            var targetBone = command.context as GameObject;
            if (targetBone == null) return;

            var window = BoneRendererSetupWindow.Instance;
            if (window == null)
            {
                Debug.LogWarning("[BoneRenderer] Setup Window is not open.");
                return;
            }

            var avatar = window.CurrentAvatar;
            var outfit = window.CurrentOutfit;

            if (avatar == null || outfit == null)
            {
                Debug.LogWarning("[BoneRenderer] Avatar or Outfit is not set in the window.");
                return;
            }

            // Verify the target bone belongs to the outfit
            if (!targetBone.transform.IsChildOf(outfit.transform) && targetBone != outfit)
            {
                 Debug.LogWarning($"[BoneRenderer] Selected object '{targetBone.name}' is not part of the active outfit '{outfit.name}'.");
                 return;
            }

            // Find matching bone in Avatar
            // Use Matcher
            var matches = OutfitBoneMapper.GetDetailedMatches(outfit, avatar);
            var match = matches.FirstOrDefault(m => m.OutfitBone == targetBone.transform);

            if (match.OutfitBone == null || match.AvatarBone == null)
            {
                // Fallback: Try name match directly if Mapper failed or wasn't exhaustive
                // This is a "Partial" feature, so maybe the mapper didn't pick it up but the user wants to force it?
                // For now, rely on Mapper.
                Debug.LogWarning($"[BoneRenderer] No matching avatar bone found for '{targetBone.name}'.");
                return;
            }

            Undo.RecordObject(targetBone.transform, "Align Bone with Avatar");
            
            // Align World Position and Rotation
            targetBone.transform.position = match.AvatarBone.position;
            targetBone.transform.rotation = match.AvatarBone.rotation;
            
            Debug.Log($"[BoneRenderer] Aligned '{targetBone.name}' to '{match.AvatarBone.name}'");
        }

        [MenuItem("GameObject/BoneRenderer Setup/Align with Avatar", true)]
        public static bool ValidateAlignWithAvatar()
        {
            // Only show if Window is open and references are set
            return BoneRendererSetupWindow.Instance != null && 
                   BoneRendererSetupWindow.Instance.CurrentAvatar != null &&
                   BoneRendererSetupWindow.Instance.CurrentOutfit != null &&
                   Selection.activeGameObject != null;
        }
    }
}
