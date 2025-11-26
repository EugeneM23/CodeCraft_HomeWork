using Atomic.Elements;
using Atomic.Entities;

namespace Game.Gameplay
{
    public class RangeWeaponFireAction : IAction
    {
        private const float IMPULSE = 2;
        private readonly IEntity _weapon;
        private readonly GameFactory _gameFactory;
        private readonly GameContext _gameContext;

        public RangeWeaponFireAction(IEntity weapon, GameContext gameContext)
        {
            _gameFactory = gameContext.GetGameFactory();
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
                SpawnUseCase.SpawnBullet(_weapon, _gameFactory, firePoint);

            if (_weapon.TryGetShellPoint(out var shellPoint))
                SpawnUseCase.SpawnShell(_gameFactory, _weapon, shellPoint, IMPULSE);

            if (_weapon.TryGetFireEvent(out var @event))
                @event.Invoke();

            _gameContext.GetPlayerCamera().GetCameraShakeEvent().Invoke();
        }
    }
}