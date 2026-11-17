using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What the AI needs to know about the body it drives: where it is, how fast it can go and how far it can jump.
    /// The navigator decides whether an edge is passable from these numbers, so the same graph works for a slow brute
    /// and for a jumping scout without touching the behavior.
    /// </summary>
    public struct AgentBodyInfo
    {
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>Against gravity (a rotated gravity frame turns it); world up by default.</summary>
        public Vector3 Up;

        public bool IsGrounded;

        /// <summary>-1 / 1 for a side-view body; 0 when facing is meaningless.</summary>
        public int Facing;

        /// <summary>
        /// Where a body in 3D looks (unit, across Up); zero — a side-view body, whose look is Facing along the right of
        /// Up. The senses' cones open around it (step 90: a 3D body's cones followed the world's +X before).
        /// </summary>
        public Vector3 Forward;

        public float Radius;
        public float Height;

        /// <summary>Top running speed.</summary>
        public float MaxSpeed;

        /// <summary>How high the feet get with a full jump (air jumps included).</summary>
        public float JumpHeight;

        /// <summary>How far a running full jump carries the body back to the same height.</summary>
        public float JumpReach;

        public bool CanJump;
        public bool CanFly;

        /// <summary>Can go up and down ladders (SetVertical).</summary>
        public bool CanClimb;

        /// <summary>Can fall through a one-way platform underfoot (DropThrough).</summary>
        public bool CanDropThrough;

        /// <summary>Has a dash, and how far one carries the body.</summary>
        public bool HasDash;
        public float DashReach;

        /// <summary>Steps up to this height are walked over without a jump.</summary>
        public float StepHeight;

        public static AgentBodyInfo Default =>
            new()
            {
                Up = Vector3.up,
                Facing = 1,
                Radius = 0.3f,
                Height = 1.6f,
                MaxSpeed = 4f
            };
    }
}
