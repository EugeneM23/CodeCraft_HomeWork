using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game
{
    public class IKHandController : MonoBehaviour
    {
        public ChainIKConstraint rightHandIK;
        public ChainIKConstraint leftHandIK;

        public void Enable(bool enable)
        {
            rightHandIK.weight = enable ? 1f : 0f;
            leftHandIK.weight = enable ? 1f : 0f;
        }
    }
}