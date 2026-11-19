using UnityEngine;

namespace Game
{
    /// <summary>
    /// The armour rules as plain math: the angle of a hit, the plate's thickness along the shell's path, and whether
    /// the shell glances off, sticks or goes through. No state, no physics.
    /// </summary>
    public static class ArmourMath
    {
        /// <summary>Steepest angle the effective thickness is taken at: at 90° the path runs along the plate.</summary>
        public const float MAX_ANGLE = 89f;

        /// <summary>
        /// Angle (deg) between the shell's path and the plate's normal: 0 head-on, 90 along the plate.
        /// <paramref name="normal"/> points out of the plate, toward the shell.
        /// </summary>
        public static float ImpactAngle(Vector3 direction, Vector3 normal)
        {
            if (direction.sqrMagnitude < 1e-8f || normal.sqrMagnitude < 1e-8f)
                return 0f;
            float cos = Vector3.Dot(-direction.normalized, normal.normalized);
            return Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>The plate's thickness along a path at <paramref name="angle"/> from its normal: nominal / cos(angle).</summary>
        public static float EffectiveThickness(float nominal, float angle) =>
            nominal / Mathf.Cos(Mathf.Min(Mathf.Abs(angle), MAX_ANGLE) * Mathf.Deg2Rad);

        /// <summary>The angle after a shell that bites in turns <paramref name="normalisation"/> degrees toward the normal.</summary>
        public static float Normalise(float angle, float normalisation) => Mathf.Max(0f, angle - Mathf.Max(0f, normalisation));

        /// <summary>Whether a shell of this calibre overmatches a plate this thick: it cannot glance off it.</summary>
        public static bool Overmatches(float calibre, float nominal, float factor) =>
            nominal <= 0f || calibre > nominal * factor;

        /// <summary>The shell's penetration on one shot: nominal ± spread, <paramref name="roll"/> in [-1, 1].</summary>
        public static float RollPenetration(float penetration, float spread, float roll) =>
            penetration * (1f + Mathf.Clamp(roll, -1f, 1f) * Mathf.Max(0f, spread));

        /// <summary>
        /// One shell against one zone.
        /// </summary>
        /// <param name="penetration">The shell's penetration on this hit (already rolled), mm.</param>
        /// <param name="calibre">The shell's calibre, mm.</param>
        /// <param name="nominal">The zone's nominal thickness, mm.</param>
        /// <param name="angle">Angle between the path and the plate's normal, deg.</param>
        public static ArmourResult Resolve(float penetration, float calibre, float nominal, float angle, ArmourKind kind,
            in ArmourRules rules)
        {
            var result = new ArmourResult { Angle = angle, Nominal = nominal, Penetration = penetration };
            if (kind == ArmourKind.Module)
            {
                result.Outcome = ArmourOutcome.ModuleHit;
                result.Effective = 0f;
                result.Remaining = penetration;
                return result;
            }

            result.Overmatched = Overmatches(calibre, nominal, rules.overmatch);
            if (!result.Overmatched && angle >= rules.ricochetAngle)
            {
                result.Outcome = ArmourOutcome.Ricochet;
                result.Effective = EffectiveThickness(nominal, angle);
                result.Remaining = penetration * Mathf.Clamp01(rules.ricochetKeep);
                return result;
            }

            result.Effective = EffectiveThickness(nominal, Normalise(angle, rules.normalisation));
            if (result.Effective >= penetration)
            {
                result.Outcome = ArmourOutcome.NoPenetration;
                result.Remaining = 0f;
                return result;
            }

            result.Outcome = kind == ArmourKind.Spaced ? ArmourOutcome.SpacedPassed : ArmourOutcome.Penetration;
            result.Remaining = penetration - result.Effective;
            return result;
        }

        /// <summary>
        /// The path after a shell bites in: turned up to <paramref name="normalisation"/> degrees toward the plate's
        /// inward normal (never past it).
        /// </summary>
        public static Vector3 NormalisedDirection(Vector3 direction, Vector3 normal, float normalisation)
        {
            Vector3 inward = -normal.normalized;
            float angle = Vector3.Angle(direction, inward);
            return Vector3.RotateTowards(direction.normalized, inward, Mathf.Min(angle, normalisation) * Mathf.Deg2Rad, 0f)
                .normalized;
        }

        /// <summary>The facing of a plate whose normal, in its part's frame, is <paramref name="local"/>: its main axis.</summary>
        public static ArmourFacing Facing(Vector3 local)
        {
            float x = Mathf.Abs(local.x), y = Mathf.Abs(local.y), z = Mathf.Abs(local.z);
            if (x < 1e-6f && y < 1e-6f && z < 1e-6f)
                return ArmourFacing.None;
            if (z >= x && z >= y)
                return local.z >= 0f ? ArmourFacing.Front : ArmourFacing.Rear;
            if (y >= x)
                return local.y >= 0f ? ArmourFacing.Top : ArmourFacing.Bottom;
            return local.x >= 0f ? ArmourFacing.Right : ArmourFacing.Left;
        }

        /// <summary>The label the lab shows for an outcome.</summary>
        public static string Label(ArmourOutcome outcome) => outcome switch
        {
            ArmourOutcome.Ricochet => "Рикошет",
            ArmourOutcome.NoPenetration => "Не пробил",
            ArmourOutcome.Penetration => "Пробитие",
            ArmourOutcome.SpacedPassed => "Экран пройден",
            ArmourOutcome.ModuleHit => "Попадание в модуль",
            _ => outcome.ToString()
        };
    }
}
