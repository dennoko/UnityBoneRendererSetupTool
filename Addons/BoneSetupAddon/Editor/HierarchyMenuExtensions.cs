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
            
            // Align World Position
            targetBone.transform.position = match.AvatarBone.position;

            // Align Rotation with minimal world-space change to avoid twisting
            if (TryGetBoneDirection(targetBone.transform, out var outfitDirection) &&
                TryGetBoneDirection(match.AvatarBone, out var avatarDirection))
            {
                var rotationDelta = Quaternion.FromToRotation(outfitDirection, avatarDirection);
                targetBone.transform.rotation = rotationDelta * targetBone.transform.rotation;
            }
            else
            {
                targetBone.transform.rotation = match.AvatarBone.rotation;
            }
            
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

        private static bool TryGetBoneDirection(Transform bone, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (bone == null)
                return false;

            var child = GetPrimaryChild(bone);
            if (child != null)
            {
                var childDelta = child.position - bone.position;
                if (childDelta.sqrMagnitude > Mathf.Epsilon)
                {
                    direction = childDelta.normalized;
                    return true;
                }
            }

            if (bone.parent != null)
            {
                var parentDelta = bone.position - bone.parent.position;
                if (parentDelta.sqrMagnitude > Mathf.Epsilon)
                {
                    direction = parentDelta.normalized;
                    return true;
                }
            }

            return false;
        }

        private static Transform GetPrimaryChild(Transform bone)
        {
            Transform best = null;
            var bestDistance = 0f;

            foreach (Transform child in bone)
            {
                var distance = (child.position - bone.position).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = child;
                }
            }

            return best;
        }
    }
}
