using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class SpawnUseCase
    {
        public static void SpawnCollisionHit(Collision collision, GameFactory gameFactory, IEntity bullet)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 position = contact.point;
            Vector3 normal = contact.normal;

            IEntity go = gameFactory.Create(bullet.GetBulletHitPrefab());
            Transform effectTransform = go.GetTransform();
            effectTransform.position = position;
            effectTransform.rotation = Quaternion.LookRotation(normal);
        }

        public static IEntity SpawnBullet(IEntity weapon, GameFactory gameFactory, Transform firePoint)
        {
            IEntity bullet = gameFactory.Create(weapon.GetBulletPrefab());

            // Bullets come from a pool, so a re-rented one already carries Damage. AddValue now
            // throws on a duplicate key instead of ignoring it, and SetValue upserts.
            bullet.SetDamage(weapon.GetDamage());
            Transform bulletTransform = bullet.GetTransform();
            bulletTransform.SetPositionAndRotation(firePoint.position, GetSpreadRotation(weapon, firePoint));
            bullet.GetMoveDirection().Value = bulletTransform.forward;

            // A pooled bullet keeps the bounces of its previous flight.
            if (bullet.HasRicochetCount())
                bullet.SetRicochetCount(0);

            PassThroughShooter(bullet, weapon);
            return bullet;
        }

        private static readonly List<Collider> BulletColliders = new();

        /// <summary>
        /// Switches collisions off between the bullet and the colliders of whoever fired it
        /// (the weapon's ShooterColliders): the helicopter's guns hang under its nose and wings and
        /// aim down and to the side through a wide spread, and their bullets struck its own hull.
        /// A bullet comes from a pool, so the pairs it was given by its previous shooter are put
        /// back first; a weapon without shooter colliders leaves the bullet hitting everything.
        ///
        /// Set on every shot, even by the same shooter: Unity forgets an ignored pair once one of
        /// the colliders is deactivated, and the pool deactivates the bullet between flights.
        /// </summary>
        public static void PassThroughShooter(IEntity bullet, IEntity weapon)
        {
            Collider[] previous = bullet.TryGetIgnoredColliders(out Collider[] p) ? p : null;
            Collider[] next = weapon.TryGetShooterColliders(out Collider[] s) ? s : null;

            if (previous == null && next == null)
                return;

            BulletColliders.Clear();
            bullet.GetGameObject().GetComponentsInChildren(false, BulletColliders);

            if (previous != next)
                SetIgnored(previous, false);
            SetIgnored(next, true);

            // An entity value cannot hold null: a bullet now fired by a plain gun drops the key.
            if (next != null)
                bullet.SetIgnoredColliders(next);
            else
                bullet.DelIgnoredColliders();
        }

        private static void SetIgnored(Collider[] shooter, bool ignore)
        {
            if (shooter == null)
                return;

            foreach (Collider own in BulletColliders)
            {
                // Once per bullet collider, not once per pair: three native calls each, and the
                // helicopter's hull alone is eight colliders, on every round.
                if (!IsLive(own))
                    continue;

                foreach (Collider other in shooter)
                    if (IsLive(other))
                        Physics.IgnoreCollision(own, other, ignore);
            }
        }

        // Unity refuses, with an error, to pair a collider that is switched off.
        private static bool IsLive(Collider collider) =>
            collider != null && collider.enabled && collider.gameObject.activeInHierarchy;

        // Spread turns the bullet about the world vertical, not the fire point's own up: the
        // weapon tilts with the fire animation, and a tilted axis would send bullets into the
        // floor or over a target's head. A weapon without Spread fires dead straight.
        private static Quaternion GetSpreadRotation(IEntity weapon, Transform firePoint)
        {
            if (!weapon.TryGetSpread(out IValue<float> spread) || spread.Value <= 0f)
                return firePoint.rotation;

            float angle = Random.Range(-spread.Value, spread.Value);
            return Quaternion.AngleAxis(angle, Vector3.up) * firePoint.rotation;
        }

        public static void SpawnShell(GameFactory gameFactory, IEntity weapon, Transform shellPoint, float impulse)
        {
            IEntity shell = gameFactory.Create(weapon.GetShellPrefab());
            shell.GetTransform().SetPositionAndRotation(shellPoint.position, shellPoint.rotation);

            // The body is placed too, not just the transform. An interpolated body — the tank's big
            // casing — is drawn from its own pose, so it went back to wherever the pool had left it
            // and fell out of the sky there. And a casing re-rented from the pool still carried the
            // velocity it had when it was put away.
            Rigidbody body = shell.GetRiggedBody();
            body.position = shellPoint.position;
            body.rotation = shellPoint.rotation;
            body.linearVelocity = Vector3.zero;
            body.AddForce((Vector3.up + shellPoint.right) * impulse, ForceMode.Impulse);

            // A casing that has a spin tumbles as it leaves; one without leaves still.
            float spin = shell.TryGetShellSpin(out float rate) ? rate : 0f;
            body.angularVelocity = EjectSpin(shellPoint.up, shellPoint.right, spin,
                Random.Range(0.7f, 1f), Random.Range(-0.5f, 0.5f));
        }

        /// <summary>
        /// Angular velocity (rad/s, magnitude <paramref name="spin"/>) for a casing leaving the
        /// ejection point. The casing lies along the point's forward, so it tumbles end over end —
        /// mostly about the point's up, with <paramref name="sideShare"/> of a flip about its right —
        /// and never spins about its own length, where turning would not show.
        /// </summary>
        public static Vector3 EjectSpin(Vector3 up, Vector3 right, float spin, float upShare, float sideShare)
        {
            if (spin <= 0f)
                return Vector3.zero;

            Vector3 axis = up * upShare + right * sideShare;
            return axis.sqrMagnitude > 1e-8f ? axis.normalized * spin : up * spin;
        }

        public static void UnSpawnEntity(in GameFactory factory, in IEntity entity) => factory.Destroy(entity);

        public static void SpawnCharacter(MonoEntity characterPrefab, Transform spawnPoint, IEntityWorld<IEntity> entityWorld,
            GameFactory gameFactory)
        {
            IEntity entity = gameFactory.Create(characterPrefab);
            entity.GetTransform().position = spawnPoint.position;
            entityWorld.Add(entity);
        }
    }
}