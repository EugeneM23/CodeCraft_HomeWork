using UnityEngine;

namespace Gameplay
{
    public class AttackController : IInitializeble, IDisposable
    {
        private InputReader _inputReader;

        [Inject]
        private void Construct(InputReader inputReader) => _inputReader = inputReader;

        public void Initialize() => _inputReader.OnFire += Fire;

        public void Dispose()
        {
            _inputReader.OnFire -= Fire;
        }

        public void Fire() => "FIRE".Log(Color.red);
    }
}