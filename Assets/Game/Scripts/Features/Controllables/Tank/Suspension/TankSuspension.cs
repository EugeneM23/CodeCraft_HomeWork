using System;
using UnityEngine;

namespace Game
{
    /// <summary>One road wheel: where its spring hangs from, what it found below, how long it is.</summary>
    public sealed class TankWheel
    {
        /// <summary>The spring's top, on the hull. The ray starts here.</summary>
        public readonly Transform Mount;

        /// <summary>The visible wheel. Moved and spun by TankTrackView only.</summary>
        public readonly Transform Visual;

        /// <summary>-1 on the left track, +1 on the right.</summary>
        public readonly int Side;

        public bool Grounded;
        public float Length;
        public float PreviousLength;
        public Vector3 Contact;
        public Vector3 Normal;

        /// <summary>What the wheel stands on; null while it hangs free.</summary>
        public Collider Ground;

        public TankWheel(Transform mount, Transform visual, float restLength)
        {
            Mount = mount;
            Visual = visual;
            Side = mount.localPosition.x < 0f ? -1 : 1;
            Length = PreviousLength = restLength;
            Normal = Vector3.up;
        }
    }

    /// <summary>
    /// The tank's running gear. TankSuspensionBehaviour fills in each wheel and the summary every
    /// physics step; the drive reads the summary, TankTrackView reads the wheels.
    /// </summary>
    public sealed class TankSuspension
    {
        public readonly TankWheel[] Wheels;
        public readonly float WheelRadius;

        /// <summary>Spring hanging free, mount to wheel centre.</summary>
        public readonly float MaxLength;

        /// <summary>The bump stop.</summary>
        public readonly float MinLength;

        /// <summary>Share of the travel the hull sinks at rest, 0..1.</summary>
        public readonly float StaticSag;

        public readonly float DampingRatio;

        // Worked out from the body's mass when the suspension starts.
        public float Stiffness;
        public float Damping;

        public int GroundedCount;
        public Vector3 ContactCentre;
        public Vector3 GroundNormal = Vector3.up;

        /// <summary>
        /// Parked and put to sleep by TankSuspensionBehaviour: no wheel is cast and nothing pushes
        /// the hull, and the drive leaves the body alone too, or the next force would wake it.
        /// Cleared, in the same physics step, once the body is woken or somebody gets in.
        /// </summary>
        public bool Resting;

        public TankSuspension(Transform[] mounts, Transform[] visuals, float wheelRadius, float maxLength,
            float minLength, float staticSag, float dampingRatio)
        {
            if (mounts == null || visuals == null || mounts.Length != visuals.Length)
                throw new ArgumentException("Every wheel mount needs a visual wheel.");

            WheelRadius = wheelRadius;
            MaxLength = maxLength;
            MinLength = minLength;
            StaticSag = Mathf.Clamp01(staticSag);
            DampingRatio = dampingRatio;

            Wheels = new TankWheel[mounts.Length];
            for (int i = 0; i < mounts.Length; i++)
                Wheels[i] = new TankWheel(mounts[i], visuals[i], RestLength);
        }

        /// <summary>How far the hull sinks under its own weight.</summary>
        public float SagDistance => StaticSag * (MaxLength - MinLength);

        public float RestLength => MaxLength - SagDistance;

        /// <summary>Share of the wheels on the ground: how much grip the tracks have.</summary>
        public float Grip => Wheels.Length == 0 ? 0f : (float)GroundedCount / Wheels.Length;
    }
}
