using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The cannon's shell types on entities: which type the cannon has loaded (switched by the player), and what a
    /// fresh shell of that type carries — its speed, damage, blast radius, ricochet rule and glow. Only explosive
    /// shells (the cannon's) take a type; machine-gun bullets are left as they are.
    /// </summary>
    public static class AmmoUseCase
    {
        /// <summary>Blast radius when the weapon has none of its own (the shell prefab's ExplosionInstaller).</summary>
        public const float DEFAULT_RADIUS = 5f;

        /// <summary>Radius (m) of the scrape a ricochet leaves in voxels: a few of them, not a crater.</summary>
        public const float SCRAPE_RADIUS = 0.12f;

        /// <summary>The cannon's loaded type, added with the config's start type on first use.</summary>
        public static IReactiveVariable<ShellKind> GetOrAdd(IEntity weapon)
        {
            if (weapon.TryGetCannonAmmo(out IReactiveVariable<ShellKind> ammo))
                return ammo;
            ammo = new ReactiveVariable<ShellKind>(TankAmmoConfig.Load().Start);
            weapon.AddCannonAmmo(ammo);
            return ammo;
        }

        /// <summary>The type the cannon has loaded (the config's start type until it is switched).</summary>
        public static ShellKind Loaded(IEntity weapon) =>
            weapon != null && weapon.TryGetCannonAmmo(out IReactiveVariable<ShellKind> ammo)
                ? ammo.Value
                : TankAmmoConfig.Load().Start;

        /// <summary>Loads the other type (HE ↔ AP). Returns the type now loaded.</summary>
        public static ShellKind Switch(IEntity weapon)
        {
            IReactiveVariable<ShellKind> ammo = GetOrAdd(weapon);
            ammo.Value = Next(ammo.Value);
            return ammo.Value;
        }

        public static ShellKind Next(ShellKind kind) =>
            kind == ShellKind.HighExplosive ? ShellKind.ArmourPiercing : ShellKind.HighExplosive;

        /// <summary>Whether a projectile is a typed cannon shell.</summary>
        public static bool IsShell(IEntity bullet) => bullet != null && bullet.HasShellAmmoKind();

        /// <summary>A shell's type; high-explosive for anything untyped.</summary>
        public static ShellKind KindOf(IEntity bullet) =>
            bullet != null && bullet.TryGetShellAmmoKind(out IVariable<ShellKind> kind) ? kind.Value : ShellKind.HighExplosive;

        public static ShellAmmo AmmoOf(IEntity bullet) => TankAmmoConfig.Load().Get(KindOf(bullet));

        /// <summary>
        /// A fresh shell takes its type's numbers (BulletShotUseCase.Prepare, after the loadout's radius). Pooled
        /// shells keep their holders: nothing is allocated after a shell's first shot of each type.
        /// </summary>
        public static void Prepare(IEntity bullet, IEntity weapon)
        {
            if (bullet == null || weapon == null || !ExplosionUseCase.IsExplosive(bullet))
                return;

            ShellKind kind = Loaded(weapon);
            ShellAmmo ammo = TankAmmoConfig.Load().Get(kind);

            bool changed = true;
            if (bullet.TryGetShellAmmoKind(out IVariable<ShellKind> holder))
            {
                changed = holder.Value != kind;
                holder.Value = kind;
            }
            else
                bullet.AddShellAmmoKind(new Variable<ShellKind>(kind));

            if (bullet.TryGetMoveSpeed(out IReactiveVariable<float> speed))
                speed.Value = ammo.speed;

            // Damage: the shell's own holder, so the type's factor does not touch the cannon's.
            if (weapon.TryGetDamage(out IReactiveVariable<int> weaponDamage))
            {
                if (!bullet.TryGetShellDamage(out IReactiveVariable<int> own))
                {
                    own = new ReactiveInt(0);
                    bullet.AddShellDamage(own);
                }

                own.Value = Mathf.RoundToInt(weaponDamage.Value * ammo.damageFactor);
                bullet.SetDamage(own);
            }

            // Blast radius: the loadout's (or the prefab's) times the type's factor; written only when it differs (boxed).
            float baseRadius = weapon.TryGetShellRadius(out IVariable<float> shellRadius) && shellRadius.Value > 0f
                ? shellRadius.Value
                : DEFAULT_RADIUS;
            float radius = baseRadius * Mathf.Max(0.05f, ammo.radiusFactor);
            if (!Mathf.Approximately(bullet.GetExplosionRadius(), radius))
                bullet.SetExplosionRadius(radius);

            if (changed)
                Recolour(bullet, ammo.glow);
        }

        /// <summary>The shell's glow and trail in its type's colour.</summary>
        private static void Recolour(IEntity bullet, Color color)
        {
            if (!bullet.TryGetGameObject(out GameObject go) || go == null)
                return;
            var glow = go.GetComponentInChildren<ShellGlowView>(true);
            if (glow != null)
                glow.SetColor(color);
        }

        /// <summary>
        /// Whether a typed shell glances off a surface whose outward normal is <paramref name="normal"/>: its type
        /// ricochets, it has ricochets left, and the angle from the normal reaches the type's threshold.
        /// </summary>
        public static bool ShouldRicochet(IEntity bullet, Vector3 direction, Vector3 normal)
        {
            ShellAmmo ammo = AmmoOf(bullet);
            if (!ammo.CanRicochet)
                return false;
            int done = bullet.HasRicochetCount() ? bullet.GetRicochetCount() : 0;
            return done < ammo.maxRicochets && ArmourMath.ImpactAngle(direction, normal) >= ammo.ricochetAngle;
        }

        /// <summary>
        /// A typed shell's ricochet: reflected off <paramref name="normal"/>, slower, a scrape and sparks where it
        /// glanced off (a few voxels on voxel ground or walls, no crater).
        /// </summary>
        public static void Ricochet(IEntity bullet, Vector3 point, Vector3 normal, Collider surface)
        {
            Vector3 incoming = bullet.GetMoveDirection().Value;
            ShellAmmo ammo = AmmoOf(bullet);
            if (bullet.HasRicochetCount())
                RicochetUseCase.Ricochet(bullet, normal);
            else
                Reflect(bullet, normal);
            if (bullet.TryGetMoveSpeed(out IReactiveVariable<float> speed))
                speed.Value *= Mathf.Clamp01(ammo.ricochetSpeedKeep);

            if (!Application.isPlaying)
                return;
            VoxelSparks.Emit(point, normal, bullet.GetMoveDirection().Value, 45);
            if (surface != null && VoxelDamage.IsVoxel(surface))
                VoxelDamage.Impact(point, incoming, SCRAPE_RADIUS, 2f, 12);
        }

        private static void Reflect(IEntity bullet, Vector3 normal)
        {
            IReactiveVariable<Vector3> direction = bullet.GetMoveDirection();
            direction.Value = Vector3.Reflect(direction.Value, normal).normalized;
            bullet.GetTransform().rotation = Quaternion.LookRotation(direction.Value);
        }

        /// <summary>
        /// The surface's real slope where a shell struck it: a ray back along the shot onto the struck collider,
        /// smoothed over a few voxels (ArmourSurfaceUseCase). The contact's normal when the ray misses.
        /// </summary>
        public static Vector3 SurfaceNormal(Collider surface, Vector3 point, Vector3 direction, Vector3 contactNormal)
        {
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : -contactNormal;
            var ray = new Ray(point - dir * 0.3f, dir);
            if (surface == null || !surface.Raycast(ray, out RaycastHit hit, 0.6f))
                return contactNormal;
            return ArmourSurfaceUseCase.SmoothNormal(hit, dir);
        }
    }
}
