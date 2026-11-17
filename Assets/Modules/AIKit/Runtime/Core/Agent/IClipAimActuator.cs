using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A body whose actions are moved by their clips (root motion of the states tagged Action, handbook §20.2), aimed at
    /// a target the way TLOU II aims a blow (§20.3, "the window of homing in on the target"): a turn in place is the
    /// clip's turn stretched to end facing the target, a blow steps as its clip does but not into the target. Optional
    /// next to IAgentActuator: a body without it is turned by the node (Face) and stands through a blow, as before.
    /// Every aim lets go by itself once the action is over.
    /// </summary>
    public interface IClipAimActuator
    {
        /// <summary>The clips of actions move and turn this body now.</summary>
        bool ActionsByClip { get; }

        /// <summary>
        /// The action now playing turns the body as its clip does, stretched to end facing the target (followed if it
        /// moves): clipAngle — how far the clip itself turns, degrees (90 for a quarter turn).
        /// </summary>
        void TurnByClipTo(Transform target, float clipAngle);

        /// <summary>
        /// The action now playing (a blow stepping forward) does not carry the body closer to the target than the
        /// body's radius and half a body of the target: the blow lands, the bodies do not go into each other.
        /// </summary>
        void KeepClipOff(Transform target);

        /// <summary>The clip is its own again (the action is taken away).</summary>
        void EndClipTurn();
    }
}
