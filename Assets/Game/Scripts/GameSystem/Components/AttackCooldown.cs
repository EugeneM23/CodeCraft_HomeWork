using UnityEngine;

namespace Gameplay
{
    public class AttackCooldown : IInitializeble, ITickable
    {
        private InputReader _inputReader;
        private float _cooldown = 0.2f;
        private bool _canAttack = true;
        private float _lastAttackTime;

        [Inject]
        private void Construct(InputReader inputReader)
        {
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            _inputReader.OnFire += TryAttack;
        }

        private void TryAttack()
        {
            if (!_canAttack)
                return;

            _canAttack = false;
            _lastAttackTime = Time.time;
        }

        public void Tick()
        {
            _canAttack.Log();
            if (!_canAttack && Time.time >= _lastAttackTime + _cooldown)
                _canAttack = true;
        }

        public bool CanAttack() => !_canAttack;
    }
}