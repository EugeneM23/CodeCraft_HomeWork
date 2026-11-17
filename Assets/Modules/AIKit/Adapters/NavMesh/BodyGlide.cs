using UnityEngine;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// The speed of a body that speeds up and brakes with its acceleration instead of jumping to a speed and back to
    /// nothing in one step — so the feet of its walk start and stop clips stay on the ground (handbook §20.2: the
    /// locomotion follows the body; the body must move the way the clips can show). A hand-held step speeds up toward
    /// the speed asked; a walking body asked another way turns its speed round (TURN_RATE) instead of braking through a
    /// standstill; with nothing asked and no path the body glides to a stop; a path that ends (cleared, stopped)
    /// hands its speed to the glide; a path that begins takes the glide's speed over.
    /// </summary>
    public sealed class BodyGlide
    {
        /// <summary>
        /// Degrees per second a moving body turns its speed toward a new direction asked, keeping its pace — a sidestep
        /// turned back the other way does not brake through a standstill (half a turn — a quarter of a second).
        /// </summary>
        public const float TURN_RATE = 720f;

        /// <summary>The speed the body has now, metres per second (flat).</summary>
        public Vector3 Velocity { get; private set; }

        private bool _wasOnPath;

        /// <summary>
        /// One step. asked — a hand-held direction times its speed (null — nothing pressed); onPath — the agent walks
        /// a path now, at pathVelocity. Returns how far to move the body by hand this step (zero on a path: the agent
        /// moves itself); handOver — a path has just begun: give the agent this speed to start from.
        /// </summary>
        public Vector3 Step(Vector3? asked, bool onPath, Vector3 pathVelocity, float acceleration, float dt,
            out bool handOver)
        {
            handOver = false;
            float change = Mathf.Max(0.01f, acceleration) * dt;
            Vector3 move = Vector3.zero;
            if (asked.HasValue)
            {
                Velocity = Toward(Velocity, Flat(asked.Value), change, TURN_RATE * dt);
                move = Velocity * dt;
                _wasOnPath = false;
            }
            else if (onPath)
            {
                handOver = !_wasOnPath && Velocity.sqrMagnitude > 1e-6f;
                if (!handOver)
                    Velocity = Flat(pathVelocity);
                _wasOnPath = true;
            }
            else
            {
                Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, change);
                move = Velocity * dt;
                _wasOnPath = false;
            }

            return move;
        }

        /// <summary>The body is put somewhere else (a teleport, a jump): no speed carries over.</summary>
        public void Halt()
        {
            Velocity = Vector3.zero;
            _wasOnPath = false;
        }

        // From a standstill, or to one, the speed changes with the acceleration — the start and stop clips' ramp. A body
        // already walking that is asked another way turns its speed round at TURN_RATE and changes only its pace with the
        // acceleration: braking to nothing and starting again took a second below the stop clip's threshold, and the
        // animator showed stop, stance and start on every turn of a step
        private static Vector3 Toward(Vector3 now, Vector3 asked, float change, float turn)
        {
            const float MOVING = 0.1f;
            if (now.magnitude < MOVING || asked.magnitude < MOVING)
                return Vector3.MoveTowards(now, asked, change);

            float pace = Mathf.MoveTowards(now.magnitude, asked.magnitude, change);
            float off = Vector3.SignedAngle(now, asked, Vector3.up);       // round the vertical: a flat turn, either way
            float step = Mathf.Clamp(off, -turn, turn);
            return Quaternion.AngleAxis(step, Vector3.up) * now.normalized * pace;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
