using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class CharacterShakeController : IContextInit<IPlayerContext>
    {
        private IEntity _character;
        private IReactiveVariable<IEntity> _weapon;
        private IEntity _currentWeapon;
        private IEntity _camera;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
            _camera = context.GetCamera();
            _weapon = _character.GetWeapon();

            _weapon.Observe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            if (_currentWeapon != null) 
                _currentWeapon.GetFireEvent().Unsubscribe(Shake);

            _currentWeapon = weapon;
            _currentWeapon.GetFireEvent().Subscribe(Shake);
        }

        private void Shake()
        {
            _camera.GetCameraShakeEvent().Invoke(_weapon.Value.GetCameraShakeArgs());
        }
    }
}