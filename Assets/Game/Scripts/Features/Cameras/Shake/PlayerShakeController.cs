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
    public class PlayerShakeController : IPlayerContextInit, IPlayerContextDispose
    {
        private IEntity _camera;

        private Subscription<IEntity> _characterSubscription;
        private HeldWeaponFireListener _weaponFire;
        private CameraShakeArgs _args;

        public void Init(IPlayerContext context)
        {
            _camera = context.GetCamera();
            _characterSubscription = context.GetCharacter().Observe(this.OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            this.ReleaseWeapon();
        }

        private void OnCharacterChanged(IEntity character)
        {
            this.ReleaseWeapon();

            if (character != null)
                _weaponFire = new HeldWeaponFireListener(character.GetWeapon(), this.Shake, this.OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            _args = null;
            weapon?.TryGetCameraShakeArgs(out _args);
        }

        private void ReleaseWeapon()
        {
            _weaponFire?.Dispose();
            _weaponFire = null;
            _args = null;
        }

        private void Shake(IEntity weapon)
        {
            if (_args != null)
                _camera.GetCameraShakeEvent().Invoke(_args);
        }
    }
}
