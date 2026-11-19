using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Voxel fire and sparks (<see cref="VoxelMuzzleFire"/>) at a gun's muzzle on every shot, off the weapon's
    /// FireEvent. On the tank for its cannon (<c>cannon</c> true: the big burst and flash), on the machine-gun weapon
    /// itself for its rounds (the small one). The fire keeps the muzzle's own speed, measured frame to frame, so a
    /// tank firing on the move does not leave it hanging behind.
    /// </summary>
    public sealed class MuzzleFireBehaviour : IEntityInit, IEntityLateTick, IEntityDispose
    {
        private readonly bool _cannon;

        private IEntity _weapon;
        private IEvent _fireEvent;
        private Transform _tracked;
        private Vector3 _lastPosition;
        private Vector3 _velocity;

        /// <param name="cannon">
        /// True: on the tank, for the weapon in its Weapon slot (the cannon). False: on the gun's own weapon entity.
        /// </param>
        public MuzzleFireBehaviour(bool cannon)
        {
            _cannon = cannon;
        }

        public void Init(IEntity entity)
        {
            if (_cannon)
            {
                if (!entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                    return;
                _weapon = weapon.Value;
            }
            else
            {
                _weapon = entity;
            }

            if (_weapon.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;
            _weapon = null;
            _tracked = null;
        }

        public void LateTick(IEntity entity, float deltaTime)
        {
            if (_weapon == null || !_weapon.TryGetFirePoint(out Transform muzzle) || muzzle == null)
                return;

            Vector3 position = muzzle.position;
            if (_tracked == muzzle && deltaTime > 0f)
                _velocity = (position - _lastPosition) / deltaTime;
            else
                _velocity = Vector3.zero;
            _tracked = muzzle;
            _lastPosition = position;
        }

        private void OnFire()
        {
            if (_weapon == null || !_weapon.TryGetFirePoint(out Transform muzzle) || muzzle == null)
                return;

            // A turret's swing makes a jump of a metre a frame look like speed: keep only what a moving hull gives.
            Vector3 carry = _velocity.sqrMagnitude < 30f * 30f ? _velocity : Vector3.zero;
            if (_cannon)
                VoxelMuzzleFire.Cannon(muzzle, carry);
            else
                VoxelMuzzleFire.MachineGun(muzzle, carry);
        }
    }
}
