using UnityEngine;

namespace Gameplay
{
    public class FireController : IInitializeble
    {
        private InputReader _inputReader;

        [Inject]
        private void Construct(InputReader inputReader) => _inputReader = inputReader;

        public void Initialize() => _inputReader.OnFire += Fire;

        private void OnDisable() => _inputReader.OnFire -= Fire;

        public void Fire() => Debug.Log("Fire");
    }
}