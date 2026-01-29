using System.Linq;
using Hays.BoneRendererSetup.Core;
using Hays.BoneRendererSetup.Matching;
using UnityEngine;

namespace Hays.BoneRendererSetup.UI
{
    /// <summary>
    /// BoneRendererSetupWindow - バリデーション
    /// </summary>
    public partial class BoneRendererSetupWindow
    {
        private bool CanSetupAvatar()
        {
            return _avatar != null && AvatarBoneMapper.IsHumanoidAvatar(_avatar);
        }

        private bool CanSetupOutfit()
        {
            return _outfit != null && _avatar != null && 
                   AvatarBoneMapper.IsHumanoidAvatar(_avatar);
        }

        private bool HasBoneRenderer(GameObject target)
        {
            return target != null && BoneRendererService.HasBoneRenderer(target);
        }

        private bool HasArmature(GameObject target)
        {
            if (target == null)
                return false;

            var armatureNames = new[] { "armature", "skeleton", "root" };
            foreach (Transform child in target.transform)
            {
                var lower = child.name.ToLowerInvariant();
                if (armatureNames.Any(n => lower.Contains(n)))
                    return true;
            }
            return false;
        }
    }
}
