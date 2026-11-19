using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The cannon kicks the hull: an impulse back along the barrel, at the muzzle. Applied there,
    /// high above the centre of mass, it rocks the hull on its springs — nose up when firing ahead,
    /// leaning away when firing to the side. Only the rocking stays: the level push is cancelled at the
    /// centre of mass, so a shot never slows the tank down.
    /// </summary>
    public sealed class TankRecoilBehaviour : IEntityInit, IEntityDispose
    {
        private readonly float _impulse;

        private Rigidbody _body;
        private Transform _firePoint;
        private IEvent _fireEvent;

        public TankRecoilBehaviour(float impulse)
        {
            _impulse = impulse;
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();

            if (!entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                return;

            weapon.Value.TryGetFirePoint(out _firePoint);

            if (weapon.Value.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;
        }

        private void OnFire()
        {
            if (_firePoint == null || _body == null || _body.isKinematic)
                return;

            Vector3 kick = -_firePoint.forward * _impulse;
            _body.AddForceAtPosition(kick, _firePoint.position, ForceMode.Impulse);

            // The kick rocks the hull (the torque of it, at the muzzle) but does not brake or shove the tank: its
            // level part is given back at the centre of mass, so the speed the tank had stays.
            Vector3 level = new(kick.x, 0f, kick.z);
            _body.AddForce(-level, ForceMode.Impulse);
        }
    }
}
