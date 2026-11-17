using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The cannon kicks the hull: an impulse back along the barrel, at the muzzle. Applied there,
    /// high above the centre of mass, it rocks the hull on its springs — nose up when firing ahead,
    /// leaning away when firing to the side. The drive takes the backward slide out again within a
    /// couple of steps; the rocking it leaves to the springs.
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

            _body.AddForceAtPosition(-_firePoint.forward * _impulse, _firePoint.position, ForceMode.Impulse);
        }
    }
}
