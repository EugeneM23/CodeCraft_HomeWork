using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A "dash button" of a body that can dash (a dodge, a hop back): the body goes exactly this far over exactly this
    /// long, past its speed and its speeding up — the distance and the time are a clip's (handbook §20.3), so the feet
    /// do not slide. Optional next to IAgentActuator: a body without it does not dash (the Dash node says Blocked).
    /// </summary>
    public interface IDashActuator
    {
        /// <summary>The way is free for this dash from where the body stands (on its ground, nothing in between).</summary>
        bool CanDash(Vector3 displacement);

        /// <summary>Starts the dash: the body is carried by displacement over time seconds, evenly.</summary>
        void Dash(Vector3 displacement, float time);

        /// <summary>A dash is under way.</summary>
        bool IsDashing { get; }
    }
}
