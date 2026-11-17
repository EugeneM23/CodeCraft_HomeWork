using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Throws one of the character's grenades of the selected type so that it lands on a world point:
    /// spends a grenade, rents one from the pool at the throw point, lights its fuse and launches it
    /// on an arc. The caller checks GrenadeThrowCondition first.
    /// </summary>
    public sealed class GrenadeThrowAction : IAction<Vector3>
    {
        // Long enough for the grenade to clear the thrower's capsule, short enough that a grenade
        // bouncing straight back off a wall still hits them.
        private const float IGNORE_THROWER_SECONDS = 0.3f;
        private const float MAX_SPIN = 10f;

        private readonly IEntity _character;
        private readonly Transform _throwPoint;
        private readonly float _maxRange;
        private readonly float _throwSpeed;
        private readonly float _minFlightTime;
        private readonly List<Collider> _throwerColliders = new();

        public GrenadeThrowAction(IEntity character, Transform throwPoint,
            float maxRange, float throwSpeed, float minFlightTime)
        {
            _character = character;
            _throwPoint = throwPoint;
            _maxRange = maxRange;
            _throwSpeed = throwSpeed;
            _minFlightTime = minFlightTime;
        }

        public void Invoke(Vector3 target)
        {
            GrenadeType type = _character.GetGrenadeSelected().Value;

            if (!_character.GetGrenadePrefabs().TryGetValue(type, out MonoEntity prefab) || prefab == null)
                return;

            _character.GetGrenadeAmmo()[type].Spend();
            _character.GetGrenadeCooldown().Reset();

            GameFactory factory = GameContext.Instance.GetGameFactory();
            IEntity grenade = factory.Create(prefab);

            Vector3 from = _throwPoint.position;
            Quaternion rotation = Random.rotation;
            Rigidbody body = grenade.GetRiggedBody();

            // The body too, not just the transform: an interpolated body would otherwise be drawn
            // sliding in from wherever the pooled grenade last lay.
            grenade.GetTransform().SetPositionAndRotation(from, rotation);
            body.position = from;
            body.rotation = rotation;
            grenade.GetGrenadeFuse().Reset();

            // Both velocities are written, so a pooled grenade that was still rolling when it went
            // back to the pool carries none of that motion into this throw.
            Vector3 landing = GrenadeTrajectory.ClampToRange(from, target, _maxRange);
            body.linearVelocity = GrenadeTrajectory.GetLaunchVelocity(from, landing, _throwSpeed, _minFlightTime,
                Physics.gravity);
            body.angularVelocity = Random.insideUnitSphere * MAX_SPIN;

            IgnoreThrower(body);
        }

        /// <summary>The grenade leaves from the shoulder, inside or right next to the thrower's colliders.</summary>
        private void IgnoreThrower(Rigidbody grenade)
        {
            if (!grenade.TryGetComponent(out Collider own))
                return;

            _character.GetGameObject().GetComponentsInChildren(_throwerColliders);

            for (int i = 0; i < _throwerColliders.Count; i++)
                if (!_throwerColliders[i].isTrigger)
                    TemporaryCollisionIgnore.Ignore(own, _throwerColliders[i], IGNORE_THROWER_SECONDS);

            _throwerColliders.Clear();
        }
    }
}
