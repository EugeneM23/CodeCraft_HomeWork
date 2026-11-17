using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Getting in and out of a vehicle.
    ///
    /// Control passes by pointing the player's Character at the vehicle. Every player controller
    /// (aim, fire, camera, shake) re-binds when Character changes, so a vehicle that carries the
    /// same keys is driven with no extra wiring. The driver is switched off while inside: hidden,
    /// not ticking, and out of reach of anything that hits.
    /// </summary>
    public static class ControllableUseCase
    {
        /// <summary>
        /// What can stop the driver stepping out: not pickups (6 Interactible), not the things that
        /// fly around (11 ThrowItem, 12 Bullet, 13 Weapon), not loose floor chips (15 Debris,
        /// 16 Rubble) — the driver steps over or through those. Layer names: ProjectSettings/TagManager.asset.
        /// </summary>
        private const int EXIT_BLOCKING_MASK = ~((1 << PhysicsLayers.Interactable) | (1 << PhysicsLayers.ThrowItem) | (1 << PhysicsLayers.Bullet) | (1 << PhysicsLayers.Weapon) | (1 << PhysicsLayers.Debris) | (1 << PhysicsLayers.Rubble));

        /// <summary>The capsule is checked this far off the ground, so the floor under it does not count.</summary>
        private const float GROUND_SKIN = 0.05f;

        /// <summary>Where the driver lands when every exit is blocked and the vehicle blows up anyway.</summary>
        private const float EJECT_HEIGHT = 1.5f;

        // A driver without a capsule (tests, a future crew member) is checked as a person-sized one.
        private const float DEFAULT_RADIUS = 0.4f;
        private const float DEFAULT_HEIGHT = 1.8f;

        public static bool CanEnter(IEntity vehicle, IEntity driver)
        {
            if (driver == null || !driver.HasPlayerTag() || driver.HasVehicleTag())
                return false;

            if (!vehicle.HasVehicleTag() || vehicle.GetDriver().Value != null)
                return false;

            return !vehicle.TryGetHealth(out Health health) || health.Exists();
        }

        public static void Enter(IEntity vehicle, IEntity driver, IReactiveVariable<IEntity> character,
            IEnumerable<IEntity> entities)
        {
            // The interaction marker follows the driver's target. The driver stops looking for
            // targets once it is switched off, so without this the "get in" marker would stay
            // hanging over the vehicle for the whole drive.
            if (driver.TryGetTargetInteractable(out IReactiveVariable<IEntity> target))
                target.Value = null;

            // A switched-off collider never reports leaving a trigger, so the enemies would keep
            // chasing an empty spot. They are handed the vehicle instead.
            Retarget(entities, driver, vehicle);

            vehicle.GetDriver().Value = driver;
            vehicle.AddPlayerTag();

            driver.GetGameObject().SetActive(false);

            character.Value = vehicle;
        }

        /// <summary>
        /// Puts the driver at the first exit point where they fit. False, with nothing changed,
        /// when every exit is blocked or the vehicle is empty.
        /// </summary>
        public static bool TryExit(IEntity vehicle, IReactiveVariable<IEntity> character, IEnumerable<IEntity> entities)
        {
            IEntity driver = vehicle.GetDriver().Value;

            if (driver == null || !TryFindExit(vehicle, driver, out Vector3 position))
                return false;

            Exit(vehicle, position, character, entities);
            return true;
        }

        /// <summary>
        /// Like <see cref="TryExit"/>, but never refuses: with every exit blocked the driver is
        /// dropped above the vehicle. Used when the vehicle is destroyed.
        /// </summary>
        public static void Eject(IEntity vehicle, IReactiveVariable<IEntity> character, IEnumerable<IEntity> entities)
        {
            IEntity driver = vehicle.GetDriver().Value;

            if (driver == null)
                return;

            if (!TryFindExit(vehicle, driver, out Vector3 position))
                position = vehicle.GetTransform().position + Vector3.up * EJECT_HEIGHT;

            Exit(vehicle, position, character, entities);
        }

        public static void Exit(IEntity vehicle, Vector3 position, IReactiveVariable<IEntity> character,
            IEnumerable<IEntity> entities)
        {
            IEntity driver = vehicle.GetDriver().Value;

            if (driver == null)
                return;

            Place(driver, position, Facing(vehicle));

            // Nobody holds the stick any more: the drive must not keep the last throttle. A ridden
            // mount has no stick, it walks on MoveDirection like the player does, and stops too.
            if (vehicle.TryGetDriveInput(out IReactiveVariable<Vector2> drive))
                drive.Value = Vector2.zero;
            if (vehicle.TryGetMoveDirection(out IReactiveVariable<Vector3> move))
                move.Value = Vector3.zero;
            if (vehicle.TryGetDriveBoost(out IReactiveVariable<bool> boost))
                boost.Value = false;
            vehicle.DelPlayerTag();
            vehicle.GetDriver().Value = null;

            Retarget(entities, vehicle, driver);

            character.Value = driver;
        }

        public static bool TryFindExit(IEntity vehicle, IEntity driver, out Vector3 position)
        {
            position = default;

            if (!vehicle.TryGetExitPoints(out Transform[] points) || points == null)
                return false;

            GetCapsule(driver, out float radius, out float height);

            foreach (Transform point in points)
            {
                if (point == null || !IsFree(point.position, radius, height))
                    continue;

                position = point.position;
                return true;
            }

            return false;
        }

        private static bool IsFree(Vector3 foot, float radius, float height)
        {
            Vector3 bottom = foot + Vector3.up * (radius + GROUND_SKIN);
            Vector3 top = foot + Vector3.up * Mathf.Max(radius + GROUND_SKIN, height - radius);

            return !Physics.CheckCapsule(bottom, top, radius, EXIT_BLOCKING_MASK, QueryTriggerInteraction.Ignore);
        }

        private static void GetCapsule(IEntity driver, out float radius, out float height)
        {
            radius = DEFAULT_RADIUS;
            height = DEFAULT_HEIGHT;

            if (!driver.TryGetRiggedBody(out Rigidbody body) || body == null)
                return;

            if (!body.TryGetComponent(out CapsuleCollider capsule))
                return;

            Vector3 scale = capsule.transform.lossyScale;
            radius = capsule.radius * Mathf.Max(scale.x, scale.z);
            height = capsule.height * scale.y;
        }

        private static Quaternion Facing(IEntity vehicle)
        {
            Vector3 forward = vehicle.GetTransform().forward;
            forward.y = 0f;

            return forward.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : Quaternion.identity;
        }

        private static void Place(IEntity driver, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = driver.GetGameObject();

            // The body is interpolated: the transform alone would be pulled back to the old physics
            // pose. The transform still has to move, because the body is switched off and only
            // picks its pose up from the transform when it comes back on.
            gameObject.transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            if (driver.TryGetRiggedBody(out Rigidbody body) && body != null)
            {
                body.position = position;
                body.rotation = rotation;

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private static void Retarget(IEnumerable<IEntity> entities, IEntity from, IEntity to)
        {
            foreach (IEntity entity in entities)
            {
                if (entity == null || !entity.HasEnemyTag())
                    continue;

                if (entity.TryGetTarget(out IReactiveVariable<IEntity> target) && target.Value == from)
                    target.Value = to;
            }
        }
    }
}
