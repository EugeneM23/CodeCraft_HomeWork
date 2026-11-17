using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A bullet that grazes a surface bounces off it and flies on with its full damage and speed;
    /// a steep hit still stops it. <c>normal</c> is the contact normal, pointing from the surface
    /// towards the bullet.
    /// </summary>
    public static class RicochetUseCase
    {
        /// <summary>
        /// Angle between the flight direction and the surface plane: 0 when skimming along it,
        /// 90 when hitting it head-on, negative when flying away from it.
        /// </summary>
        public static float GetSurfaceAngle(Vector3 direction, Vector3 normal) =>
            90f - Vector3.Angle(direction, -normal);

        /// <summary>
        /// True when the bullet already flies away from the surface. One physics step reports a
        /// contact per collider touched, so right after a bounce the bullet still gets the contact
        /// of a neighbouring brick or of the same wall; reflecting it again would send it back.
        /// </summary>
        public static bool IsMovingAway(Vector3 direction, Vector3 normal) =>
            Vector3.Dot(direction, normal) >= 0f;

        public static bool IsEnabled(IEntity bullet) =>
            bullet.TryGetRicochetMaxCount(out int maxCount) && maxCount > 0;

        public static bool CanRicochet(IEntity bullet, Vector3 normal)
        {
            if (!IsEnabled(bullet) || bullet.GetRicochetCount() >= bullet.GetRicochetMaxCount())
                return false;

            Vector3 direction = bullet.GetMoveDirection().Value;
            return GetSurfaceAngle(direction, normal) <= bullet.GetRicochetMaxAngle();
        }

        public static void Ricochet(IEntity bullet, Vector3 normal)
        {
            IReactiveVariable<Vector3> moveDirection = bullet.GetMoveDirection();
            Vector3 direction = Vector3.Reflect(moveDirection.Value, normal).normalized;
            moveDirection.Value = direction;

            // The trail and the model follow the transform, so turn it to where the bullet now flies.
            bullet.GetTransform().rotation = Quaternion.LookRotation(direction);

            // The bullet flies by transform, but its Rigidbody is dynamic: the solver has just
            // bounced it off the wall, and that velocity would drift it from now on. It never
            // mattered before, because the bullet went back to the pool on its first contact.
            if (bullet.TryGetRiggedBody(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            bullet.SetRicochetCount(bullet.GetRicochetCount() + 1);
        }
    }
}
