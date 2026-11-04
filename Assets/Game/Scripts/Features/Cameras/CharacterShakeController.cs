using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Shakes the camera when the weapon in hand fires.
    ///
    /// Both links are kept live through subscriptions rather than snapshotted: the character can be
    /// replaced on respawn, and the weapon it holds changes on every pick-up. Both subscriptions are
    /// released on dispose — previously neither was, so a pooled weapon kept a Shake handler aimed
    /// at a dead camera after every scene reload, and one shot fired N shakes.
    ///
    /// The shake arguments are resolved once per weapon instead of on every shot.
    /// </summary>
    public class CharacterShakeController : IPlayerContextInit, IPlayerContextDispose
    {
        private IEntity _camera;

        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;

        private IReactiveVariable<IEntity> _weaponSource;
        private Subscription<IEntity> _weaponSubscription;

        private IEntity _currentWeapon;
        private CameraShakeArgs _args;

        public void Init(IPlayerContext context)
        {
            _camera = context.GetCamera();
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            UnbindWeaponSource();
        }

        private void OnCharacterChanged(IEntity character)
        {
            UnbindWeaponSource();

            if (character == null) return;

            _weaponSource = character.GetWeapon();
            _weaponSubscription = _weaponSource.Observe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            UnbindWeapon();

            if (weapon == null) return;

            _currentWeapon = weapon;
            weapon.TryGetCameraShakeArgs(out _args);
            weapon.GetFireEvent().Subscribe(Shake);
        }

        private void UnbindWeaponSource()
        {
            _weaponSubscription.Dispose();
            _weaponSubscription = default;
            _weaponSource = null;

            UnbindWeapon();
        }

        private void UnbindWeapon()
        {
            // On teardown Unity destroys objects in an arbitrary order, so the weapon may already
            // be gone by the time the context disposes. A destroyed MonoEntity is still a non-null
            // interface reference — Unity's null-overload does not apply through IEntity — so
            // reading a value off it throws, and building the exception message throws again on
            // the destroyed object's name. IsValid is the check that survives that.
            if (_currentWeapon is { IsValid: true })
                _currentWeapon.GetFireEvent().Unsubscribe(Shake);

            _currentWeapon = null;
            _args = null;
        }

        private void Shake()
        {
            if (_args != null)
                _camera.GetCameraShakeEvent().Invoke(_args);
        }
    }
}
