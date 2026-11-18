using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Turns the turret towards RotateDirection — the value PlayerAimController writes for
    /// the player's character, so the cursor aims the turret with no input code of its own.
    ///
    /// Two transforms move together. The pivot carries the cannon and its fire point and only
    /// ever yaws: SpawnUseCase takes the shell's rotation straight off the fire point, and a fire
    /// point that pitched with the hull would send shells into the floor. The visible turret sits
    /// on the swaying hull and copies the pivot's yaw, so it rocks with the hull while the shell
    /// still leaves level.
    /// </summary>
    public sealed class TurretAimBehaviour : IEntityInit, IEntityTick
    {
        private IValue<Vector3> _aim;
        private IValue<float> _speed;
        private Transform _pivot;
        private Transform _visual;

        // The hull's body, when it has one. The last frame found the turret on its aim with the body
        // asleep (a parked tank: TankSuspensionBehaviour), so the hull did not turn under it: with
        // the same aim again, both rotations would be written again as they are, every frame.
        private Rigidbody _body;
        private bool _settled;
        private Vector3 _settledAim;

        public void Init(IEntity entity)
        {
            _aim = entity.GetRotateDirection();
            _speed = entity.GetTurretRotationSpeed();
            _pivot = entity.GetTurretPivot();
            entity.TryGetTurretVisual(out _visual);
            entity.TryGetRiggedBody(out _body);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            Vector3 direction = _aim.Value;
            direction.y = 0f;

            bool asleep = _body != null && _body.IsSleeping();
            if (asleep && _settled && direction.Equals(_settledAim))
                return;

            // Stunned (the elite's leap): the turret stands where it is, turning only with the hull, and
            // counts as off its aim, so it turns again once the stun is over.
            bool stunned = TankStunUseCase.IsStunned(entity);
            bool onAim = !stunned;
            if (direction.sqrMagnitude > 1e-4f && !stunned)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion current = _pivot.rotation;

                // A zero angle is when RotateTowards hands back the target itself: on its aim.
                onAim = Quaternion.Angle(current, target) == 0f;
                _pivot.rotation = Quaternion.RotateTowards(current, target, _speed.Value * deltaTime);
            }

            // Every frame, not only while turning: the hull turns under a turret that holds its aim.
            if (_visual != null)
                _visual.localRotation = Quaternion.Euler(0f, _pivot.localEulerAngles.y, 0f);

            _settled = asleep && onAim;
            _settledAim = direction;
        }
    }
}
