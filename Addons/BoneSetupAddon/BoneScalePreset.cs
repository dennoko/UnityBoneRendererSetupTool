using System.Collections.Generic;
using UnityEngine;

namespace Hays.BoneRendererSetup.Addons
{
    [CreateAssetMenu(fileName = "BoneScalePreset", menuName = "BoneRenderer/Scale Preset")]
    public class BoneScalePreset : ScriptableObject
    {
        public string Title;
        public List<BoneScaleData> Scales = new List<BoneScaleData>();
    }

    [System.Serializable]
    public class BoneScaleData
    {
        public string BoneName;
        public Vector3 Scale;
    }
}
