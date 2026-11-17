using System.Collections.Generic;
using Atomic.Entities;
using Game.Voxels;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Detonates an explosive projectile: falloff damage to every damageable entity in range,
    /// a hole punched in a destructible wall (bricks near the blast knocked out, the rest intact),
    /// a shove for loose debris, a flash and a camera shake.
    /// </summary>
    public static class ExplosionUseCase
    {
        private static readonly List<ParticleSystemRenderer> CrumbRenderers = new();

        /// <summary>
        /// The voxel crater is this share of the damage radius: the tank shell (5 m) digs 1.6 m.
        /// The whole radius would flatten a house with one shot.
        /// </summary>
        private const float VOXEL_CRATER_SHARE = 0.32f;

        /// <summary>Speed (m/s) voxel debris and loose pieces leave the crater with.</summary>
        private const float VOXEL_BLAST_SPEED = 9f;

        /// <summary>Share of the damage radius blown clear of voxel smoke: the tank shell (5 m) clears 3.5 m.</summary>
        private const float VOXEL_SMOKE_SHARE = 0.7f;

        public static bool IsExplosive(IEntity projectile) => projectile.HasExplosionRadius();

        public static void Explode(IEntity projectile, Vector3 point, Collider hit, GameFactory factory)
        {
            float radius = projectile.GetExplosionRadius();

            DamageInRadius(projectile, point, radius);
            ExplosionEvents.Raise(point, radius);

            if (projectile.TryGetExplosionBomb(out RayfireBomb bomb) && bomb != null)
            {
                // The bomb only shoves loose pieces already lying around. Its own damage pass hit
                // every brick within the whole radius, split dozens of them in one frame and brought
                // the wall down; the wall itself is handled by BreachDestructibles below.
                bomb.applyDamage = false;
                bomb.range = radius;
                bomb.transform.position = point;
                ShoveLoosePieces(bomb, point, radius);
            }

            // After the bomb, so its random shove does not add to the breach velocities.
            BreachDestructibles(projectile, point);

            // A crater in anything made of voxels: walls, buildings, the tank's skin.
            VoxelDamage.Blast(point, radius * VOXEL_CRATER_SHARE, VOXEL_BLAST_SPEED);

            // The blast blows voxel smoke away round it, the way an HE grenade clears a CS2 smoke.
            VoxelSmoke.Blast(point, radius * VOXEL_SMOKE_SHARE);

            if (projectile.TryGetExplosionVfxPrefab(out MonoEntity vfxPrefab) && vfxPrefab != null)
            {
                IEntity vfx = factory.Create(vfxPrefab);
                vfx.GetTransform().SetPositionAndRotation(point, Quaternion.identity);
            }

            SpawnCrumbs(projectile, point, hit, factory);

            // A crater and a fountain of mud, when it went off on or just above mud.
            MudImpactUseCase.Blast(point, radius);

            if (projectile.TryGetCameraShakeArgs(out CameraShakeArgs shake))
                CameraShakeUseCase.ShakePlayerCamera(shake);
        }

        /// <summary>
        /// Sets the bomb off without two of RayfireBomb's side effects. It gathers every collider in
        /// range on its layers and looks each body up in a list, so next to a wall it would grind
        /// through the hundreds of standing pieces on Destructible, which it never moves (they are
        /// kinematic): that layer is left out, unless one of the few loose bodies RayFire leaves on it
        /// is within reach (<see cref="LooseOnDestructibleLayer"/>). And it reseeds Unity's random
        /// generator to vary its push (Random.InitState(1)), which would make every breach after it
        /// throw its pieces the same way: the game's own sequence is put back.
        /// </summary>
        private static void ShoveLoosePieces(RayfireBomb bomb, Vector3 point, float radius)
        {
            int mask = bomb.mask;
            if (!LooseOnDestructibleLayer.AnyWithin(point, radius + Mathf.Abs(bomb.heightOffset)))
                bomb.mask = mask & ~(1 << PhysicsLayers.Destructible);

            Random.State random = Random.state;
            try
            {
                bomb.Explode(0f);
            }
            finally
            {
                Random.state = random;
                bomb.mask = mask;
            }
        }

        private static void DamageInRadius(IEntity projectile, Vector3 point, float radius)
        {
            float minFactor = projectile.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;

            BlastAreaUseCase.ForEachDamageable(point, radius, null, (target, distanceShare) =>
                DamageUseCase.TakeDamage(target,
                    new TakeDamageArgs(projectile, Mathf.Lerp(1f, minFactor, distanceShare))));
        }

        /// <summary>
        /// Punches a hole in a destructible wall around the blast, with the projectile's breach
        /// settings: see <see cref="BreachUseCase"/>.
        /// </summary>
        private static void BreachDestructibles(IEntity projectile, Vector3 point)
        {
            if (!projectile.TryGetExplosionBreachRadius(out float radius) || radius <= 0f)
                return;

            float speed = projectile.TryGetExplosionBreachSpeed(out float breachSpeed) ? breachSpeed : 5f;
            float splitChance = projectile.TryGetExplosionSplitChance(out float chance) ? chance : 0f;

            BreachUseCase.Breach(point, radius, speed, splitChance);
        }

        /// <summary>
        /// A spray of small crumbs at the impact, aimed along the shot (the effect's +Z) and drawn
        /// with the material of what was hit, so a brick wall sheds brick and concrete sheds
        /// concrete. Skipped on characters: they bleed through their own damage effects.
        /// </summary>
        private static void SpawnCrumbs(IEntity projectile, Vector3 point, Collider hit, GameFactory factory)
        {
            if (!projectile.TryGetExplosionCrumbsPrefab(out MonoEntity prefab) || prefab == null || hit == null)
                return;

            // Mud throws its own clumps (MudImpactUseCase.Blast), not chips of the cube under it.
            if (MudImpactUseCase.IsMud(hit))
                return;

            IEntity target = BlastAreaUseCase.FindEntity(hit);
            if (target != null && target.HasDamageableTag())
                return;

            Vector3 direction = projectile.TryGetMoveDirection(out var move) ? move.Value : Vector3.zero;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.up;

            IEntity crumbs = factory.Create(prefab);
            Transform effect = crumbs.GetTransform();
            effect.SetPositionAndRotation(point, Quaternion.LookRotation(direction.normalized));

            Renderer surface = hit.GetComponent<Renderer>();
            Material material = surface != null ? surface.sharedMaterial : null;
            if (material == null)
                return;

            effect.GetComponentsInChildren(CrumbRenderers);
            for (int i = 0; i < CrumbRenderers.Count; i++)
                CrumbRenderers[i].sharedMaterial = material;
            CrumbRenderers.Clear();
        }
    }
}
