using UnityEngine;

namespace Gameplay
{
    public class FireController : IInitializeble
    {
        public void Initialize()
        {
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnFire += Fire;
        }

        private void OnDisable()
        {
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnFire -= Fire;
        }

        public void Fire() => Debug.Log("Fire");
    }
}