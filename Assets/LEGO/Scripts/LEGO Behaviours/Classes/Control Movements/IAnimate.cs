using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Controls
{
    public interface IAnimate
    {
        void AnimationSetup(List<MeshRenderer> scopedPartRenderers);
    }
}
