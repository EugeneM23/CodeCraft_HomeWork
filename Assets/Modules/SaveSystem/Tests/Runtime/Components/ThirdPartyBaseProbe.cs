using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// Stands for a component of a framework (a DI context, a kernel): a setting and a flag it keeps to itself. Tests
    /// name this assembly as third-party code and derive a "game script" from it in another assembly.
    /// </summary>
    public class ThirdPartyBaseProbe : MonoBehaviour
    {
        public int BaseShown = 1;

#pragma warning disable CS0414 // read through reflection only
        private bool _hasRun;
#pragma warning restore CS0414

        public void Run() => _hasRun = true;
    }
}
