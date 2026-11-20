using System;
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

        /// <summary>An HE shell's blast against a house's skeleton: its hardness counts this many times less.</summary>
        private const float HE_SKELETON_DAMAGE = 2f;

        /// <summary>The spark particles inside the shell's explosion effect (FX_RocketExplosion).</summary>
        private const string SPARKS_CHILD = "Sparks";

        public static bool IsExplosive(IEntity projectile) => projectile.HasExplosionRadius();

        /// <summary>
        /// Whether a shell's blast damages what is round it (WeaponSettings.shellAreaDamage). Off, the shell has dealt its
        /// damage to the enemy it hit (BulletCollisionBehaviour, BulletEnemyHitUseCase) and the blast only digs, pushes,
        /// flashes and shakes.
        /// </summary>
        public static bool AreaDamage => CombatConfig.Load().Weapons.shellAreaDamage;

        /// <summary>
        /// Whether this projectile's blast damages what is round it: every shell with <see cref="AreaDamage"/> on, and an
        /// HE shell always (the user), though with the switch off an HE blast only damages enemies, never the player's
        /// tank or other friendly things. The callers skip the direct hit's own damage then: the blast deals it.
        /// </summary>
        public static bool HitsArea(IEntity projectile) =>
            AreaDamage || ShellHitSparksUseCase.IsHighExplosive(projectile);

        /// <param name="normal">The struck surface's normal where known (zero: up); the HE spark burst fans out of it.</param>
        /// <param name="flesh">It struck a monster: blood and flesh fly instead of the HE sparks.</param>
        public static void Explode(IEntity projectile, Vector3 point, Collider hit, GameFactory factory,
            Vector3 normal = default, bool flesh = false)
        {
            float radius = projectile.GetExplosionRadius();

            if (AreaDamage)
            {
                DamageInRadius(projectile, point, radius, false);
                DamageEnemiesWithoutColliders(projectile, point, radius);
            }
            else if (HitsArea(projectile))
            {
                // An HE shell with the general switch off: the blast takes down the monsters round it, not the tank.
                DamageInRadius(projectile, point, radius, true);
                DamageEnemiesWithoutColliders(projectile, point, radius);
            }
            // The shell's own blast sound (weapons.shell_hit); the generic one for this Exploded is skipped then.
            GameSignals.RaiseShellExploded(point);
            ExplosionEvents.Raise(point, radius);

            // The «Мощный фугас» skill of the tank that fired an HE shell (1: none): faster gore and debris, a bigger bite
            float power = ShellPowerUseCase.Of(projectile);

            // An HE shell: a big spray of sparks and a hot point, like a bullet's hit only more.
            bool he = ShellHitSparksUseCase.IsHighExplosive(projectile);
            if (he)
            {
                if (flesh)
                    ShellHitSparksUseCase.EmitFlesh(point, normal, power);
                else
                    ShellHitSparksUseCase.Emit(point, normal);
            }

            // A crater in anything made of voxels: walls, buildings, the tank's skin. An HE shell hits a house's
            // skeleton (piers, beams) twice as hard as anything else does; the skill bites a bigger crater (volume
            // × power), hits the skeleton harder and throws the debris faster.
            Bite(radius, he, power, out float crater, out float debrisSpeed, out float skeleton);
            VoxelBody.SkeletonDamage = skeleton;
            try
            {
                VoxelDamage.Blast(point, crater, debrisSpeed);
            }
            finally
            {
                VoxelBody.SkeletonDamage = 1f;
            }

            // The blast blows voxel smoke away round it, the way an HE grenade clears a CS2 smoke.
            VoxelSmoke.Blast(point, radius * VOXEL_SMOKE_SHARE);
            VoxelPuffField.Blast(point, radius * VOXEL_SMOKE_SHARE);

            if (projectile.TryGetExplosionVfxPrefab(out MonoEntity vfxPrefab) && vfxPrefab != null)
            {
                IEntity vfx = factory.Create(vfxPrefab);
                Transform effect = vfx.GetTransform();
                effect.SetPositionAndRotation(point, Quaternion.identity);
                // On a monster the blast throws blood and flesh, not sparks: the effect's own spark particles are off
                // for that burst (the effect is pooled: set every time).
                Transform sparks = effect.Find(SPARKS_CHILD);
                if (sparks != null)
                    sparks.gameObject.SetActive(!flesh);
            }

            SpawnCrumbs(projectile, point, hit, factory);

            // A crater and a fountain of mud, when it went off on or just above mud.
            MudImpactUseCase.Blast(point, radius);

            if (projectile.TryGetCameraShakeArgs(out CameraShakeArgs shake))
                CameraShakeUseCase.ShakePlayerCamera(shake);

            // The ricochet skill: a cannon shell bounces on to the next enemy outside its blast.
            ShellBounceUseCase.TryBounce(projectile, point, radius, factory);
        }

        /// <summary>
        /// The crater a blast of <paramref name="radius"/> bites in voxels: its radius, the speed its debris leaves with
        /// and how hard it hits a house's skeleton. An HE shell's <paramref name="power"/> («Мощный фугас»,
        /// <see cref="ShellPowerUseCase"/>) scales the bitten volume (the radius by its cube root), the debris speed and the
        /// skeleton damage; power 1 is the blast without the skill.
        /// </summary>
        public static void Bite(float radius, bool he, float power, out float craterRadius, out float debrisSpeed,
            out float skeletonDamage)
        {
            if (!he)
                power = 1f;
            craterRadius = radius * VOXEL_CRATER_SHARE * ShellPowerUseCase.BiteRadius(power);
            debrisSpeed = VOXEL_BLAST_SPEED * power;
            skeletonDamage = he ? HE_SKELETON_DAMAGE * power : 1f;
        }

        /// <summary>
        /// The blast's damage to the enemies without colliders (the horde) through the level's EnemyTargets; entity
        /// enemies already took theirs from <see cref="DamageInRadius"/>. The horde ALSO still takes its own fixed
        /// damage from <see cref="ExplosionEvents.Exploded"/> (HordeBehaviour) until the AI team drops it.
        /// </summary>
        private static void DamageEnemiesWithoutColliders(IEntity projectile, Vector3 point, float radius)
        {
            if (!projectile.TryGetDamage(out var damage) || damage.Value <= 0)
                return;
            if (!GameContext.TryGetInstance(out GameContext context) ||
                !context.TryGetEnemyTargets(out IEnemyTargets targets) || targets is not EnemyTargets enemyTargets)
                return;

            float minFactor = projectile.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;
            enemyTargets.DamageAreaNonPhysical(point, radius, damage.Value, minFactor);
        }

        private static readonly Func<IEntity, Collider, bool> EnemiesOnly = (target, _) => target.HasEnemyTag();

        private static void DamageInRadius(IEntity projectile, Vector3 point, float radius, bool enemiesOnly)
        {
            float minFactor = projectile.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;

            BlastAreaUseCase.ForEachDamageable(point, radius, enemiesOnly ? EnemiesOnly : null, (target, distanceShare) =>
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
