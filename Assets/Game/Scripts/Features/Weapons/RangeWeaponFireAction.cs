using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class RangeWeaponFireAction : IAction
    {
        private const float IMPULSE = 2;
        private readonly IEntity _weapon;
        private readonly GameContext _gameContext;
        private GameFactory _gameFactory;

        /// <remarks>
        /// The factory is looked up on the first shot, not here. A weapon placed in the scene — the
        /// tank's cannon — is installed when the entity world collects the scene, which can come
        /// before [Context] has installed its GameFactory. Reading it here threw, and the throw cut
        /// the weapon's install short: no FireCondition, and every frame the fire key was held
        /// raised KeyNotFoundException. Weapons spawned from the catalog never hit this only because
        /// they are created after the context is up.
        /// </remarks>
        public RangeWeaponFireAction(IEntity weapon, GameContext gameContext)
        {
            _gameContext = gameContext;
            _weapon = weapon;
        }

        public void Invoke()
        {
            _gameFactory ??= _gameContext.GetGameFactory();

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
        }
    }
}