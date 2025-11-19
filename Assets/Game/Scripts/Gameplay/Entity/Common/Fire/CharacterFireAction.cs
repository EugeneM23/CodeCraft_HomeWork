using Atomic.Elements;
using Atomic.Entities;

namespace Game.Gameplay
{
    public class CharacterFireAction : IAction
    {
        private readonly IEntity _character;
        private readonly GameContext _gameContext;
        private readonly IReactiveVariable<IEntity> _weapon;

        public CharacterFireAction(IEntity character)
        {
            _character = character;
            _weapon = _character.GetWeapon();
            _gameContext = GameContext.Instance;
        }

        public void Invoke()
        {
            if (_weapon.Value.TryGetAmmo(out var ammo))
                ammo.Spend();

            if (_weapon.Value.TryGetWeaponFireRate(out var fireRate))
                fireRate.Reset();

            if (_weapon.Value.TryGetFirePoint(out var firePoint))
                FireBulletUseCase.SpawnBullet(_weapon.Value, _gameContext, firePoint);

            if (_weapon.Value.TryGetFireEvent(out var @event))
                @event.Invoke();

            _character.GetAnimator().SetTrigger("Attack");
        }
    }
}