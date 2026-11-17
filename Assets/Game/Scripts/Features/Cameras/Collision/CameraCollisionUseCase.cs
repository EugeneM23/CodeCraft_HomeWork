using System;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    public static class CameraCollisionUseCase
    {
        /// <summary>
        /// The follow camera rises over buildings (voxel houses, Victorian houses, towers) and
        /// nothing else: not the ground, trees, plain walls or what is knocked off them.
        /// </summary>
        public static readonly Predicate<Collider> IsBuilding = collider =>
        {
            VoxelStructure structure = collider.GetComponentInParent<VoxelStructure>();
            return structure != null && structure.IsBuilding;
        };

        /// <summary>
        /// Whether a collider can stand in the camera's way at all. Never: a trigger; anything under
        /// <paramref name="ignore"/> (the tank or the character the camera follows); a body that
        /// moves (a knocked-off voxel chunk, another tank, a casing), so the camera does not jump
        /// every time a piece flies past.
        /// </summary>
        public static bool Blocks(Collider collider, Transform ignore)
        {
            if (collider == null || collider.isTrigger)
                return false;

            if (ignore != null && collider.transform.IsChildOf(ignore))
                return false;

            Rigidbody body = collider.attachedRigidbody;
            return body == null || body.isKinematic;
        }

        /// <summary>
        /// Whether a camera at <paramref name="camera"/> sees <paramref name="target"/> with a tube of
        /// <paramref name="radius"/> round the line free of obstacles — wide enough that the whole
        /// tank shows, not only the point it is followed by. Swept from the camera towards the target,
        /// stopping <paramref name="radius"/> short of it, so the followed body and the ground under
        /// it are never in the way. An obstacle the sphere already overlaps counts: a house right at
        /// the camera hides the target as surely as one between.
        ///
        /// An obstacle is a collider on <paramref name="mask"/> that <see cref="Blocks"/> and that
        /// <paramref name="isObstacle"/> accepts (null accepts all), and only one below the camera
        /// (<see cref="IsAbove"/>): a roof or an overhang over the camera never moves it.
        /// </summary>
        public static bool Sees(
            Vector3 camera,
            Vector3 target,
            float radius,
            int mask,
            Transform ignore,
            RaycastHit[] buffer,
            Predicate<Collider> isObstacle = null)
        {
            Vector3 toTarget = target - camera;
            float length = toTarget.magnitude - radius;
            if (length <= 0f)
                return true;

            int count = Physics.SphereCastNonAlloc(
                camera, radius, toTarget.normalized, buffer, length, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = buffer[i];
                Collider collider = hit.collider;
                if (Blocks(collider, ignore) && (isObstacle == null || isObstacle(collider)) && !IsAbove(hit, camera))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether what a sweep from <paramref name="camera"/> met lies above the camera. A hit
        /// along the sweep is above when its point is. One the sphere already overlapped at the start
        /// comes with no point; it is above when the nearest point of its collider is. A camera inside
        /// a collider is its own nearest point: that is not above, the camera is in it.
        /// </summary>
        public static bool IsAbove(RaycastHit hit, Vector3 camera)
        {
            if (hit.distance > 0f)
                return hit.point.y > camera.y;

            Collider collider = hit.collider;
            // ClosestPoint takes only convex shapes; a concave mesh is judged by its bounds.
            Vector3 nearest = collider is MeshCollider { convex: false }
                ? collider.bounds.ClosestPoint(camera)
                : collider.ClosestPoint(camera);
            return nearest.y > camera.y;
        }

        /// <summary>
        /// The least height, in <paramref name="step"/>-metre steps, the camera has to rise straight
        /// up from <paramref name="camera"/> before it <see cref="Sees"/> the <paramref name="target"/>
        /// over whatever is in the way; <paramref name="cleared"/> is false when even
        /// <paramref name="maxRise"/> does not do it (the target is under a roof), and
        /// <paramref name="maxRise"/> is returned.
        /// </summary>
        public static float RiseToClear(
            Vector3 camera,
            Vector3 target,
            float radius,
            int mask,
            Transform ignore,
            RaycastHit[] buffer,
            Predicate<Collider> isObstacle,
            float maxRise,
            float step,
            out bool cleared)
        {
            maxRise = Mathf.Max(maxRise, 0f);
            step = Mathf.Max(step, 0.1f);

            for (float rise = 0f; ; rise = Mathf.Min(rise + step, maxRise))
            {
                if (Sees(camera + Vector3.up * rise, target, radius, mask, ignore, buffer, isObstacle))
                {
                    cleared = true;
                    return rise;
                }

                if (rise >= maxRise)
                {
                    cleared = false;
                    return maxRise;
                }
            }
        }

        /// <summary>
        /// Eases <paramref name="current"/> towards <paramref name="target"/>: up at
        /// <paramref name="upSpeed"/> (infinity: at once), down at <paramref name="downSpeed"/>.
        /// Frame-rate independent.
        /// </summary>
        public static float Ease(float current, float target, float upSpeed, float downSpeed, float deltaTime)
        {
            float speed = target >= current ? upSpeed : downSpeed;
            if (float.IsPositiveInfinity(speed))
                return target;

            float t = 1f - Mathf.Exp(-speed * deltaTime);
            return Mathf.Lerp(current, target, t);
        }
    }
}
