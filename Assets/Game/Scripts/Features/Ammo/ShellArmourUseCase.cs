using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// An armour-piercing shell (a real one, an entity) striking an armoured tank (TankArmour): the same rules as
    /// the lab — it glances off steep plates, sticks in thick ones, or punches through and goes on inside to the
    /// crew and modules until the far wall stops it. Every step is reported to the tank (TankArmour.Hit).
    /// </summary>
    public static class ShellArmourUseCase
    {
        /// <summary>What the shell does next.</summary>
        public enum Step
        {
            /// <summary>Not an armoured tank: the usual hit.</summary>
            NotArmour,

            /// <summary>Glanced off: it flies on along the reflection.</summary>
            Ricocheted,

            /// <summary>Through this collider: it flies on, the collider let by.</summary>
            PassedOn,

            /// <summary>Stuck in the plate or spent inside: it goes back to the pool, no blast.</summary>
            Stopped
        }

        /// <summary>A shell's state inside a tank, kept per shell by BulletCollisionBehaviour.</summary>
        public struct State
        {
            public TankArmour Inside;
            public Vector3 Entry;

            /// <summary>Penetration left, mm; below 0 — not rolled yet (a fresh shot).</summary>
            public float Penetration;

            public static State Fresh => new() { Penetration = -1f };
        }

        /// <summary>A shell that just went through a plate is not struck by the same plate within this (m).</summary>
        private const float PASS_THROUGH = 0.35f;

        /// <summary>Radius (m) of the hole a penetration punches (tough armour halves the bite).</summary>
        public const float HOLE_RADIUS = 0.26f;

        public static Step Resolve(IEntity bullet, Collider collider, Vector3 point, Vector3 contactNormal, ref State state)
        {
            if (AmmoUseCase.KindOf(bullet) != ShellKind.ArmourPiercing)
                return Step.NotArmour;
            TankArmour armour = TankArmour.Find(collider);
            if (armour == null)
                return Step.NotArmour;

            ShellAmmo ammo = AmmoUseCase.AmmoOf(bullet);
            Vector3 dir = bullet.GetMoveDirection().Value.normalized;
            Vector3 normal = AmmoUseCase.SurfaceNormal(collider, point, dir, contactNormal);
            if (!armour.Classify(collider, point, normal, out string zone, out float nominal, out ArmourKind kind))
                return Step.NotArmour;
            VoxelBody body = collider.GetComponentInParent<VoxelBody>();

            if (state.Penetration < 0f)
                state.Penetration = ArmourMath.RollPenetration(ammo.penetration, 0.08f, Random.Range(-1f, 1f));

            // Inside: the plate it just came through again, a module, or the far wall that stops it.
            if (state.Inside == armour && kind != ArmourKind.Module)
                return Vector3.Distance(point, state.Entry) < PASS_THROUGH ? Step.PassedOn : Step.Stopped;

            ArmourResult result = ArmourMath.Resolve(state.Penetration, ammo.calibre, nominal, ArmourMath.ImpactAngle(dir, normal),
                kind, armour.Rules);
            var hit = new ArmourHit
            {
                Point = point, Normal = normal, Direction = dir, Zone = zone, Kind = kind, Result = result, Armour = armour,
                Body = body, Collider = collider
            };
            armour.Report(hit);

            switch (result.Outcome)
            {
                case ArmourOutcome.Ricochet:
                    int done = bullet.HasRicochetCount() ? bullet.GetRicochetCount() : 0;
                    if (done >= ammo.maxRicochets)
                    {
                        VoxelSparks.Emit(point, normal, dir, 35);
                        return Step.Stopped;
                    }

                    state.Penetration = result.Remaining;
                    AmmoUseCase.Ricochet(bullet, point, normal, null);
                    return Step.Ricocheted;
                case ArmourOutcome.NoPenetration:
                    VoxelSparks.Emit(point, normal, dir, 35);
                    if (body != null)
                        VoxelDamage.Punch(point, dir, 0.07f, 2f, 6, body);
                    return Step.Stopped;
                case ArmourOutcome.ModuleHit:
                    if (body != null)
                        VoxelDamage.Punch(point, dir, 0.25f, 4f, 40, body);
                    return Step.PassedOn;
                default: // penetration, spaced armour passed
                    VoxelSparks.Emit(point, normal, dir, 40);
                    if (body != null)
                        VoxelDamage.Punch(point, dir, result.Outcome == ArmourOutcome.SpacedPassed ? HOLE_RADIUS * 0.6f : HOLE_RADIUS,
                            6f, 60, body);
                    state.Penetration = result.Remaining;
                    if (result.Outcome == ArmourOutcome.Penetration)
                    {
                        state.Inside = armour;
                        state.Entry = point;
                    }

                    return Step.PassedOn;
            }
        }
    }
}
