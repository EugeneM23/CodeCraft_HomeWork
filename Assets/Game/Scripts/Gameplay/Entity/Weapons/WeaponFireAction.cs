using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class WeaponFireAction : IAction
    {
        private readonly IEntity _weapon;
        private readonly GameContext _gameContext;
        private readonly Transform _firePoint;

        public WeaponFireAction(IEntity weapon)
        {
            _firePoint = weapon.GetFirePoint();
            _gameContext = GameContext.Instance;
            _weapon = weapon;
        }

        public void Invoke()
        {
            _weapon.GetFireEvent().Invoke();
            _weapon.GetWeaponFireRate().Reset();
            SpawnBulletUseCase.SpawnBullet(_weapon, _gameContext, _firePoint);
        }
    }
}