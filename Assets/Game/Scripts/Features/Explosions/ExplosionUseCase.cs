using System.Collections.Generic;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Detonates an explosive projectile: falloff damage to every damageable entity in range,
    /// a crater in the voxels, crumbs and mud, a flash and a camera shake.
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
            GameSignals.RaiseExploded(point, radius);

            DamageInRadius(projectile, point, radius);

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

        private static void DamageInRadius(IEntity projectile, Vector3 point, float radius)
        {
            float minFactor = projectile.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;

            BlastAreaUseCase.ForEachDamageable(point, radius, null, (target, distanceShare) =>
                DamageUseCase.TakeDamage(target,
                    new TakeDamageArgs(projectile, Mathf.Lerp(1f, minFactor, distanceShare))));
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
