using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class WeaponFireAction : IAction
    {
        private readonly IEntity _weapon;
        private readonly GameContext _gameContext;

        public WeaponFireAction(IEntity weapon)
        {
            _weapon = weapon;
            _gameContext = GameContext.Instance;
        }

        public void Invoke()
        {
            if (_weapon.TryGetAmmo(out var ammo))
                ammo.Spend();

            if (_weapon.TryGetWeaponFireRate(out var fireRate))
                fireRate.Reset();

            if (_weapon.TryGetFirePoint(out var firePoint))
                FireBulletUseCase.SpawnBullet(_weapon, _gameContext, firePoint);

            if (_weapon.TryGetFireEvent(out var @event))
                @event.Invoke();
        }
    }
}