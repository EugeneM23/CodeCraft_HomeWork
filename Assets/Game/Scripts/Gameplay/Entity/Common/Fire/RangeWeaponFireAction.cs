using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class RangeWeaponFireAction : IAction
    {
        private const float IMPULSE = 2;
        private readonly IEntity _weapon;
        private readonly GameContext _gameContext;

        public RangeWeaponFireAction(IEntity weapon, GameContext gameContext)
        {
            _gameContext = gameContext;
            _weapon = weapon;
        }

        public void Invoke()
        {
            if (_weapon.TryGetAmmo(out var ammo))
                ammo.Spend();

            if (_weapon.TryGetWeaponCooldown(out var cooldown))
                cooldown.Reset();

            if (_weapon.TryGetFirePoint(out var firePoint))
            {
                FireBulletUseCase.SpawnBullet(_weapon, _gameContext, firePoint);
            }

            if (_weapon.TryGetShellPoint(out var shellPoint))
            {
                IEntity shell = FireBulletUseCase.SpawnShell(_weapon, _gameContext, shellPoint);
                shell.GetRiggedBody().AddForce((Vector3.up + shellPoint.right) * IMPULSE, ForceMode.Impulse);
            }

            if (_weapon.TryGetFireEvent(out var @event))
                @event.Invoke();

            _gameContext.GetPlayerCamera().GetCameraShakeEvent().Invoke();
        }
    }
}