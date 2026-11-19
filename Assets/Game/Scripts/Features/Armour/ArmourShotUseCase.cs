using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// An armour-piercing shell's flight through armour, traced at once along rays: it glances off steep plates
    /// (flying on along the reflection, weaker), stops in plates thicker along its path than it pierces, goes
    /// through spaced armour losing penetration, and after a penetration goes on a short way inside, striking
    /// modules and crew, until the far wall stops it. Damage is not dealt here: the caller plays the events.
    /// </summary>
    public static class ArmourShotUseCase
    {
        /// <summary>How far (m) a shell flies at most, ricochets included.</summary>
        public const float RANGE = 120f;

        /// <summary>How far (m) a shell goes on inside a tank after it pierced the armour.</summary>
        public const float INSIDE_REACH = 2.5f;

        /// <summary>Most ricochets one shell makes.</summary>
        public const int MAX_RICOCHETS = 2;

        /// <summary>A body the shell just went through is not struck again within this (m): the plate's far face.</summary>
        private const float PASS_THROUGH = 0.35f;

        private const int MAX_EVENTS = 16;

        private static readonly RaycastHit[] s_hits = new RaycastHit[32];

        /// <summary>
        /// Traces a shell from <paramref name="from"/> along <paramref name="direction"/>.
        /// </summary>
        /// <param name="roll">Where in its spread this shot's penetration falls, -1..1.</param>
        /// <param name="hits">Gets the events in order (cleared first).</param>
        /// <param name="path">Gets the path's corners: the start, every hit, the end (cleared first).</param>
        /// <param name="mask">Layers the shell strikes.</param>
        /// <param name="explosive">A high-explosive shell: it never glances off and bursts on the first thing it strikes.</param>
        public static void Trace(Vector3 from, Vector3 direction, in ArmourShell shell, float roll, List<ArmourHit> hits,
            List<Vector3> path, int mask = ~0, bool explosive = false)
        {
            hits.Clear();
            path.Clear();
            path.Add(from);
            Vector3 pos = from;
            Vector3 dir = direction.normalized;
            float pen = ArmourMath.RollPenetration(shell.penetration, shell.spread, roll);
            float range = RANGE;
            float travelled = 0f;
            int ricochets = 0;
            bool inside = false;
            TankArmour insideOf = null;
            Transform passed = null;
            float passedFor = 0f;

            for (int step = 0; step < MAX_EVENTS; step++)
            {
                int count = Physics.RaycastNonAlloc(pos, dir, s_hits, range, mask, QueryTriggerInteraction.Ignore);
                int best = -1;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit h = s_hits[i];
                    if (passed != null && h.distance < passedFor && Owner(h.collider) == passed)
                        continue;
                    if (best < 0 || h.distance < s_hits[best].distance)
                        best = i;
                }

                if (best < 0)
                {
                    path.Add(pos + dir * range);
                    return;
                }

                RaycastHit hit = s_hits[best];
                travelled += hit.distance;
                range -= hit.distance;
                path.Add(hit.point);

                TankArmour armour = TankArmour.Find(hit.collider);
                Vector3 normal = armour != null ? ArmourSurfaceUseCase.SmoothNormal(hit, dir, ArmourSurfaceUseCase.RADIUS, mask) : hit.normal;
                var record = new ArmourHit
                {
                    Point = hit.point, Normal = normal, Direction = dir, Distance = travelled, Armour = armour,
                    Collider = hit.collider, Body = hit.collider.GetComponentInParent<VoxelBody>()
                };

                if (armour == null || !armour.Classify(hit.collider, hit.point, normal, out string zone, out float nominal,
                        out ArmourKind kind))
                {
                    // The ground, the table, a wall: the shell stops there.
                    record.Armour = null;
                    record.Result = new ArmourResult
                    {
                        Outcome = ArmourOutcome.NoPenetration, Angle = ArmourMath.ImpactAngle(dir, normal), Penetration = pen
                    };
                    hits.Add(record);
                    return;
                }

                // Inside a tank the shell is spent on the first plate it meets from within (the far wall).
                if (inside && kind != ArmourKind.Module && armour == insideOf)
                    return;

                float angle = ArmourMath.ImpactAngle(dir, normal);
                ArmourRules rules = armour.Rules;
                if (explosive)
                    rules.ricochetAngle = float.MaxValue;
                ArmourResult result = ArmourMath.Resolve(pen, shell.calibre, nominal, angle, kind, rules);
                record.Zone = zone;
                record.Kind = kind;
                record.Result = result;
                hits.Add(record);
                if (explosive)
                    return;

                switch (result.Outcome)
                {
                    case ArmourOutcome.Ricochet:
                        if (++ricochets > MAX_RICOCHETS)
                            return;
                        dir = Vector3.Reflect(dir, normal).normalized;
                        // Off the plate: the reflection of a stair-stepped face may still dip into it.
                        if (Vector3.Dot(dir, normal) < 0.05f)
                            dir = (dir + normal * 0.1f).normalized;
                        pen = result.Remaining;
                        pos = hit.point + normal * 0.02f;
                        passed = null;
                        break;
                    case ArmourOutcome.NoPenetration:
                        return;
                    case ArmourOutcome.Penetration:
                        pen = result.Remaining;
                        dir = ArmourMath.NormalisedDirection(dir, normal, rules.normalisation);
                        pos = hit.point + dir * 0.01f;
                        passed = Owner(hit.collider);
                        passedFor = PASS_THROUGH;
                        if (!inside)
                            range = Mathf.Min(range, INSIDE_REACH);
                        inside = true;
                        insideOf = armour;
                        break;
                    default: // spaced armour passed, a module struck: on through it
                        pen = result.Remaining;
                        pos = hit.point + dir * 0.01f;
                        passed = Owner(hit.collider);
                        passedFor = result.Outcome == ArmourOutcome.ModuleHit ? INSIDE_REACH : PASS_THROUGH;
                        break;
                }

                if (range <= 0.01f)
                {
                    path.Add(pos);
                    return;
                }
            }
        }

        private static Transform Owner(Collider collider)
        {
            var body = collider.GetComponentInParent<VoxelBody>();
            return body != null ? body.transform : collider.transform;
        }
    }
}
