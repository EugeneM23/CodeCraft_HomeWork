using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// How a body gets over an edge it executes (handbook §13.3: a wall climbed, a jump taken), as its clips do it — the
    /// numbers are read from the character's climbing and jumping clips by an editor utility (§20.3; the AI Animator's
    /// "Measure"), never typed. A body without them gets over an edge its plain way: a straight climb at its own speed,
    /// a jump with no take-off or landing time.
    /// </summary>
    public interface ITraversalMotion
    {
        /// <summary>The way up a wall, as the climbing clip goes; false — none measured.</summary>
        bool TryClimbUp(out ClimbMotion climb);

        /// <summary>
        /// The way down a wall when the body climbs down: a clip of its own (the way up played back); false — the
        /// character has no climbing-down clip.
        /// </summary>
        bool TryClimbDown(out ClimbMotion climb);

        /// <summary>The take-off and the landing of a jump, as the jumping clips go; false — none measured.</summary>
        bool TryJump(out JumpMotion jump);
    }

    /// <summary>
    /// A climb up a wall as its clip goes: how long the clip moves the body, how high it climbs and how far forward it
    /// goes from where it starts (its distance from the wall's face) to where it stands on the top, and the part of
    /// each done at every point of the clip (0…1 over 0…1 of its time).
    /// </summary>
    [Serializable]
    public struct ClimbMotion
    {
        /// <summary>Seconds the clip moves the body (the standing tail of the clip left out).</summary>
        public float Time;

        /// <summary>Metres the clip climbs.</summary>
        public float Height;

        /// <summary>Metres the clip goes forward, from where it starts to where it stands on the top.</summary>
        public float Reach;

        /// <summary>Metres from the wall's face the clip starts at (where it stands when its hands are on the edge).</summary>
        public float FromWall;

        /// <summary>The part of the height climbed at each point of the clip; empty — evenly.</summary>
        public AnimationCurve Rise;

        /// <summary>The part of the way forward done at each point of the clip; empty — evenly.</summary>
        public AnimationCurve Advance;

        /// <summary>The part of the height climbed at this point of the clip (0…1).</summary>
        public float RiseAt(float along) => Fraction(Rise, along);

        /// <summary>The part of the way forward done at this point of the clip (0…1).</summary>
        public float AdvanceAt(float along) => Fraction(Advance, along);

        // A part of the way (0…1) at this point of the clip (0…1): the clip's own curve, a straight line with none
        private static float Fraction(AnimationCurve curve, float along)
        {
            along = Mathf.Clamp01(along);
            if (curve == null || curve.length < 2)
                return along;

            return curve.Evaluate(along);
        }
    }

    /// <summary>The take-off and the landing of a jump as its clips go.</summary>
    [Serializable]
    public struct JumpMotion
    {
        /// <summary>Seconds from the start of the jump's clip to the moment the feet leave the ground.</summary>
        public float TakeOff;

        /// <summary>Seconds into the landing clip the feet come down at (the animator enters the landing there).</summary>
        public float Touch;

        /// <summary>Seconds the body stands where it landed (the landing clip after the feet touch).</summary>
        public float Landing;
    }

    /// <summary>
    /// The climbing and jumping clips of a character, measured in the editor and kept by its body (the body's own data,
    /// handbook §13.2–13.4: an edge is executed by the body). Measured anew when the clips change (the body's "Measure
    /// again", or the AI Animator's).
    /// </summary>
    [Serializable]
    public sealed class MeasuredTraversal : ITraversalMotion
    {
        /// <summary>What it was measured from (the animator controller's name); empty — never measured.</summary>
        public string MeasuredFrom = "";

        /// <summary>
        /// The controller and the clips it was measured from, in one line (the editor's): another line now — the clips
        /// changed since, measure again.
        /// </summary>
        public string Fingerprint = "";

        public bool HasClimb;
        public ClimbMotion Climb;

        /// <summary>The controller has a climbing-down clip (the way up played back).</summary>
        public bool HasClimbDown;

        public bool HasJump;
        public JumpMotion Jump;

        /// <summary>Anything was measured.</summary>
        public bool IsMeasured => HasClimb || HasJump;

        public bool TryClimbUp(out ClimbMotion climb)
        {
            climb = Climb;
            return HasClimb && Climb.Time > 0f;
        }

        public bool TryClimbDown(out ClimbMotion climb)
        {
            climb = Climb;
            return HasClimb && HasClimbDown && Climb.Time > 0f;
        }

        public bool TryJump(out JumpMotion jump)
        {
            jump = Jump;
            return HasJump;
        }

        /// <summary>One line for the inspector: what was measured, from what.</summary>
        public string Describe()
        {
            if (!IsMeasured)
                return "not measured: a straight climb at the body's speed, a jump with no take-off or landing";

            string climb = HasClimb ? $"climb up {Climb.Time:0.00} s" : "no climb";
            string down = HasClimbDown ? "down ✓" : "down by a jump";
            string jump = HasJump ? "jump ✓" : "no jump clips";
            return $"measured from {MeasuredFrom}: {climb} · {down} · {jump}";
        }

        public MeasuredTraversal Clone()
        {
            var copy = (MeasuredTraversal)MemberwiseClone();
            copy.Climb.Rise = Climb.Rise != null ? new AnimationCurve(Climb.Rise.keys) : null;
            copy.Climb.Advance = Climb.Advance != null ? new AnimationCurve(Climb.Advance.keys) : null;
            return copy;
        }
    }

    /// <summary>
    /// A body that had its climbing numbers typed into it before step 90 (C20): the migration takes them when there is no
    /// clip to measure them from. Delete with the migration.
    /// </summary>
    public interface ILegacyTraversal
    {
        /// <summary>The numbers the body had, as measured data; false — it had none.</summary>
        bool TryTakeLegacyTraversal(out MeasuredTraversal legacy);

        /// <summary>The old numbers are moved: emptied, so the migration never takes them twice.</summary>
        void ClearLegacyTraversal();
    }

    /// <summary>
    /// The plain physics of getting over an edge, shared by every body (handbook §13.3: a fall is ballistic — done when
    /// landed below). Gravity is given by the body (a NavMesh body: Physics.gravity — the user's word, C20: the fall is
    /// gravity, nothing speeds it up); the core itself asks no physics (invariant 7).
    /// </summary>
    public static class Ballistics
    {
        /// <summary>
        /// Seconds a body falls this many metres down under this gravity, leaving at this speed up (0 — it steps off).
        /// </summary>
        public static float FallTime(float drop, float upSpeed, float gravity)
        {
            gravity = Mathf.Max(0.1f, gravity);
            upSpeed = Mathf.Max(0f, upSpeed);
            return (upSpeed + Mathf.Sqrt(upSpeed * upSpeed + 2f * gravity * Mathf.Max(0f, drop))) / gravity;
        }

        /// <summary>
        /// A jump from one point to another under gravity, rising to this height over the higher of the two (the apex
        /// clearance): the speed up it leaves with and the seconds in the air.
        /// </summary>
        public static void Arc(Vector3 from, Vector3 to, float clearance, float gravity, out float upSpeed, out float time)
        {
            gravity = Mathf.Max(0.1f, gravity);
            float apex = Mathf.Max(from.y, to.y) + Mathf.Max(0.01f, clearance);
            upSpeed = Mathf.Sqrt(2f * gravity * (apex - from.y));
            time = upSpeed / gravity + Mathf.Sqrt(2f * (apex - to.y) / gravity);
        }

        /// <summary>Where a body that left at this speed up is, this many seconds into its flight (height only).</summary>
        public static float HeightAt(float startHeight, float upSpeed, float gravity, float seconds) =>
            startHeight + upSpeed * seconds - 0.5f * Mathf.Max(0.1f, gravity) * seconds * seconds;
    }
}
