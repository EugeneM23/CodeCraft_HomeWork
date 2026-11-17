using System.Collections.Generic;
using Atomic.Entities;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Bridges our physical bullets to RayFire. A bullet is a moving collider, while RayFire's
    /// damage/activation pipeline is driven by RayfireGun's raycast, so on contact we fire the
    /// bullet's own RayfireGun along its flight direction from just before the contact point.
    /// RayfireGun then does everything the plugin docs describe: damage, impact activation of
    /// prefragmented shards, connectivity checks, impact force, debris/dust and the flash.
    /// </summary>
    public static class DestructionUseCase
    {
        // How far before the contact point the ray starts. Must stay ahead of the surface even
        // when the bullet penetrated a little during the physics step.
        private const float RAY_BACKOFF = 0.3f;

        // Pieces heavier than this (kg) are pushed by impulse, so they barely move from a bullet.
        // Whole sticky bricks are 25 kg; their shards (12-17 kg) and every other shard stay below.
        private const float HEAVY_MASS = 20f;

        /// <param name="impactFactor">
        /// Share of the gun's damage and push this contact applies: 1 for a direct hit, less for a
        /// bullet that only grazes the surface and bounces off. The lucky knockout is a direct-hit thing.
        /// </param>
        public static bool TryImpact(IEntity bullet, Collision collision, float impactFactor = 1f)
        {
            if (!bullet.TryGetDestructionImpact(out RayfireGun gun) || gun == null)
                return false;

            if (!IsDestructible(collision.collider))
                return false;

            ContactPoint contact = collision.GetContact(0);

            Vector3 direction = bullet.TryGetMoveDirection(out var moveDirection)
                ? moveDirection.Value
                : Vector3.zero;

            if (direction.sqrMagnitude < 0.0001f)
                direction = -contact.normal;

            direction.Normalize();

            // RayfireGun pushes with ForceMode.VelocityChange, which ignores mass: a 25 kg brick or
            // a two-ton chunk would fly off as fast as a pebble. For heavy pieces treat the gun
            // strength as an impulse instead, so the push is strength / mass. Light shards
            // (concrete, glass, gallery bricks: a few kg) keep the full push and still scatter.
            // The bullet is wider than a brick, so the collider it touched first may be a neighbour.
            // RayfireGun acts on whatever its ray hits, so find that piece with the same ray.
            Vector3 origin = contact.point - direction * RAY_BACKOFF;
            bool rayHitWall = Physics.Raycast(origin, direction, out RaycastHit rayHit, gun.maxDistance, gun.mask, QueryTriggerInteraction.Ignore);
            Rigidbody body = rayHitWall ? rayHit.collider.attachedRigidbody : collision.collider.attachedRigidbody;
            RayfireRigid hitRigid = body != null ? body.GetComponent<RayfireRigid>() : null;

            float strength = gun.strength;
            float push = strength * impactFactor;
            gun.strength = body != null && body.mass > HEAVY_MASS ? push / body.mass : push;

            // A wall that breaks in chunks loses what the shot reaches as chunks, released and pushed
            // here. RayfireGun would knock out every piece in its radius one by one and push the
            // pieces' own rigidbodies, which RayFire is replacing with the chunks'.
            if (KnockOutChunks(gun, hitRigid, rayHitWall ? rayHit.point : contact.point, direction, push))
                gun.strength = 0f;

            // Lucky hit: top the damage up to exactly what the piece still holds, so this shot
            // knocks it out however little damage it took before.
            float damage = gun.damage;
            gun.damage = damage * impactFactor;
            bool directHit = impactFactor >= 1f;
            if (directHit && hitRigid != null && hitRigid.damage.en
                && bullet.TryGetDestructionKnockoutChance(out float knockoutChance)
                && Random.value < knockoutChance)
                gun.damage = Mathf.Max(damage, hitRigid.damage.max - hitRigid.damage.cur);

            gun.Shoot(origin, direction);
            gun.strength = strength;
            gun.damage = damage;

            // When the shot splits the piece, RayfireGun tries to push the new shards by raycasting
            // them, but their colliders are not in the physics scene until the next step, so the
            // push is lost. Push the shards here, away from the impact point, with the full
            // (unscaled by mass) push of this contact.
            // Only a single piece split into its own shards: a cluster that broke into groups of
            // bricks keeps the weak, mass-scaled push, or whole groups would fly off like cardboard.
            if (hitRigid != null && hitRigid.objTp == ObjectType.Mesh && hitRigid.lim.demolished && hitRigid.HasFragments)
                ScatterShards(hitRigid, contact.point + direction * SHARD_BURST_DEPTH, push);

            return true;
        }

        // How deep behind the surface the shards burst from: a little inside the piece, so they
        // spray both back at the shooter and out through the hole.
        private const float SHARD_BURST_DEPTH = 0.05f;

        private static readonly Collider[] Reach = new Collider[64];
        private static readonly List<RayfireRigid> ReachedPieces = new();
        private static readonly List<Rigidbody> ReleasedBodies = new();
        private static readonly List<int> ReleasedChunks = new();

        /// <summary>
        /// Knocks the pieces within the gun's radius out of a wall with <see cref="BreakInChunks"/>
        /// and pushes what came out along the shot, the way RayfireGun pushes a piece: a velocity
        /// change, scaled down by mass only for heavy bodies. False when there was nothing to knock
        /// out that way, and the gun should handle the hit itself.
        /// </summary>
        private static bool KnockOutChunks(RayfireGun gun, RayfireRigid hit, Vector3 point, Vector3 direction, float push)
        {
            if (hit == null || hit.meshRoot == null || gun.radius <= 0f || !hit.act.imp)
                return false;

            if (hit.simTp != SimType.Kinematic && hit.simTp != SimType.Inactive)
                return false;

            if (!hit.meshRoot.TryGetComponent(out BreakInChunks wall))
                return false;

            ReachedPieces.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, gun.radius, Reach, gun.mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody reached = Reach[i].attachedRigidbody;
                RayfireRigid piece = reached != null ? reached.GetComponent<RayfireRigid>() : Reach[i].GetComponent<RayfireRigid>();

                if (piece != null && piece.meshRoot == hit.meshRoot && piece.act.imp && !ReachedPieces.Contains(piece))
                    ReachedPieces.Add(piece);
            }

            wall.Release(ReachedPieces, point, ReleasedBodies, ReleasedChunks);

            for (int i = 0; i < ReleasedBodies.Count; i++)
            {
                Rigidbody body = ReleasedBodies[i];
                float speed = body.mass > HEAVY_MASS ? push / body.mass : push;
                body.AddForce(direction * speed, ForceMode.VelocityChange);
            }

            ReachedPieces.Clear();
            return ReleasedBodies.Count > 0;
        }

        private static void ScatterShards(RayfireRigid piece, Vector3 burstPoint, float strength)
        {
            for (int i = 0; i < piece.fragments.Count; i++)
            {
                Rigidbody shard = piece.fragments[i] != null ? piece.fragments[i].physics.rb : null;
                if (shard == null || shard.isKinematic)
                    continue;

                // Same push RayfireGun applies: a velocity change away from the point, mass ignored,
                // so light chips fly as far as heavy ones.
                shard.AddExplosionForce(strength, burstPoint, 0f, 0f, ForceMode.VelocityChange);
            }
        }

        private static bool IsDestructible(Collider collider)
        {
            if (collider == null)
                return false;

            // Same lookup RayfireGun uses: a fragment's RayfireRigid sits next to its Rigidbody.
            Rigidbody body = collider.attachedRigidbody;
            Transform owner = body != null ? body.transform : collider.transform;

            return owner.GetComponent<RayfireRigid>() != null
                   || collider.GetComponentInParent<RayfireRigidRoot>() != null;
        }
    }
}
