using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What an agent occupies of the world, as the objects layer says it (step 90: an Interact that took a cover, a gun,
    /// a perch): the character's status reads it for its states Occupying and In Cover. Null on the context — nobody says,
    /// and those states have no data.
    /// </summary>
    public interface IOccupancy
    {
        /// <summary>The object of the world the agent uses now (behind a cover, at a gun, on a perch); null — none.</summary>
        Transform Occupied { get; }

        /// <summary>The object it occupies is a cover.</summary>
        bool InCover { get; }
    }

    /// <summary>What the conditions and the status of step 90 ask of the context.</summary>
    public sealed partial class AgentContext : IOccupancy
    {
        /// <summary>
        /// The object the agent is pointed at for the length of an ask (AimAt) — a decision's condition asked by the
        /// Brain is pointed at the Brain's target; null — no ask points it anywhere now.
        /// </summary>
        public Transform AimedAt => _aim;

        /// <summary>
        /// The object a question about "the Brain's target" is about: the one the agent is pointed at (a decision's
        /// condition), else the Brain's target while a decision's chain runs (a Branch of that chain); null — none.
        /// </summary>
        public Transform BrainsTarget => _aim != null ? _aim : BrainTarget;

        /// <summary>
        /// What the agent occupies of the world: by default the context itself (Occupied, InCover — what its last Interact
        /// took, AgentContext.Objects.cs); a test may give its own.
        /// </summary>
        public IOccupancy Occupancy
        {
            get => _occupancy ?? this;
            set => _occupancy = value;
        }

        private IOccupancy _occupancy;

        private ParameterChanges _parameterChanges;

        /// <summary>
        /// What Set Parameter changed of the agent's body for the length of the decision in charge (made on first ask): the
        /// body's own values, given back when the decision leaves.
        /// </summary>
        public ParameterChanges ParameterChanges => _parameterChanges ??= new ParameterChanges(this);
    }
}
