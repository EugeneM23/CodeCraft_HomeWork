using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The barrel recoils on every shot: it slams back through the mantlet in a few hundredths of
    /// a second, sits there a moment, then the recuperator runs it forward again into battery,
    /// easing out of the back and into place.
    ///
    /// Readable over realistic: from the follow camera, behind the muzzle flash and the shake, a
    /// barrel that starts coming home at once is gone before the eye finds it. The hold and the
    /// slow start of the return keep it visibly short while the smoke clears.
    ///
    /// Only the visible gun moves. The fire point stays where it is, so a shot fired while the
    /// barrel is still running out leaves from the same place as any other.
    /// </summary>
    public sealed class GunRecoilBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private readonly Transform _gun;
        private readonly float _distance;
        private readonly float _kickTime;
        private readonly float _holdTime;
        private readonly float _returnTime;

        private IEvent _fireEvent;
        private Vector3 _battery;
        private float _elapsed = float.MaxValue;

        /// <param name="gun">The barrel group, sliding along its own local Z.</param>
        /// <param name="distance">How far (m) the barrel runs back.</param>
        /// <param name="holdTime">Seconds it stays fully back before running out again.</param>
        public GunRecoilBehaviour(Transform gun, float distance, float kickTime, float holdTime, float returnTime)
        {
            _gun = gun;
            _distance = distance;
            _kickTime = kickTime;
            _holdTime = holdTime;
            _returnTime = returnTime;
        }

        /// <summary>
        /// The barrel's offset (m) along its axis <paramref name="elapsed"/> seconds after a shot:
        /// 0 in battery, −<paramref name="distance"/> fully back.
        /// </summary>
        public static float Offset(float elapsed, float distance, float kickTime, float holdTime, float returnTime)
        {
            if (elapsed <= 0f)
                return 0f;

            if (elapsed < kickTime)
                return -distance * (elapsed / kickTime);

            float run = (elapsed - kickTime - holdTime) / Mathf.Max(returnTime, 1e-4f);
            if (run <= 0f)
                return -distance;
            if (run >= 1f)
                return 0f;

            float returned = run * run * (3f - 2f * run);
            return -distance * (1f - returned);
        }

        public void Init(IEntity entity)
        {
            _battery = _gun.localPosition;

            if (entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) && weapon.Value != null &&
                weapon.Value.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;

            if (_gun != null)
                _gun.localPosition = _battery;
        }

        private void OnFire() => _elapsed = 0f;

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_elapsed > _kickTime + _holdTime + _returnTime)
                return;

            _elapsed += deltaTime;
            _gun.localPosition = _battery +
                                 Vector3.forward * Offset(_elapsed, _distance, _kickTime, _holdTime, _returnTime);
        }
    }
}
