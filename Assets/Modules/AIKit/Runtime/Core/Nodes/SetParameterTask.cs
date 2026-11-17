using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>A parameter of the character's body the graph may change for a while (Set Parameter).</summary>
    public enum BodyParameter
    {
        /// <summary>Its full speed (the pace it walks at stays a part of it).</summary>
        RunSpeed,

        /// <summary>How fast it turns.</summary>
        TurnSpeed,

        /// <summary>How far it takes to get up to speed and to stop (its acceleration is worked out from it).</summary>
        RampDistance
    }

    /// <summary>
    /// Set Parameter (step 90, C11; spec 10 — the goal of step 34, "enraged: runs faster", without copies on the
    /// blackboard): multiplies a parameter of the character's body — Run Speed ×, Turn Speed ×, Ramp Distance × — for as
    /// long as the decision whose chain ran it is in charge; when that decision leaves (AgentContext.DecisionLeft), the
    /// parameter goes back to what the body had. The body stays the one owner of the value (invariant 10): the block
    /// keeps the body's own value and writes base × Value through the part that keeps it (IAgentNumbers); the pace a
    /// block walks at stays a part of the speed, and an acceleration worked out from the ramp distance follows it. A
    /// second Set Parameter of the same parameter in the decision sets its own multiple of the same base (it does not
    /// multiply what the first set). Back To Base — the body's own value now. Health is never written. A body that has no
    /// such parameter is not changed (the node does nothing). Instant: done at once.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Life/Set Parameter", "Task", "aikit.task.set-parameter", Success = "Done", Simple = true,
        NeverFails = true)]
    public sealed class SetParameterTask : AgentTask
    {
        /// <summary>Which parameter of the body.</summary>
        public BodyParameter Parameter = BodyParameter.RunSpeed;

        /// <summary>How many times the body's own value (1.5 — half as much again); a number or a variable.</summary>
        public BlackboardValue<float> Value = 1.5f;

        /// <summary>On — the parameter goes back to the body's own value now (Value is not used).</summary>
        public bool BackToBase;

        public override bool IsInstant => true;

        public override string Requirement => "lasts while the decision is in charge";

        /// <summary>The name a body gives the parameter among its numbers.</summary>
        public static string NumberOf(BodyParameter parameter) => parameter switch
        {
            BodyParameter.TurnSpeed => BodyNumbers.TURN_SPEED,
            BodyParameter.RampDistance => BodyNumbers.RAMP_DISTANCE,
            _ => BodyNumbers.RUN_SPEED
        };

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            AICharacter agent = context?.Agent;
            if (agent == null)
                return TaskStatus.Success;

            ParameterChanges changes = context.ParameterChanges;
            if (BackToBase)
            {
                changes.Restore(Parameter);
                return TaskStatus.Success;
            }

            if (agent.TryNumberPart(NumberOf(Parameter), out IAgentNumbers part, out int index))
                changes.Set(Parameter, part, index, Mathf.Max(0f, Value.Get(context)));
            return TaskStatus.Success;                  // a body with no such parameter: nothing to change
        }
    }

    /// <summary>
    /// What Set Parameter changed of one agent's body for the length of the decision in charge (one per agent, on its
    /// context): the body's own value of each parameter while a multiple of it is set — whichever Set Parameter node set
    /// it — and the wait for the decision to leave, when every one of them goes back.
    /// </summary>
    public sealed class ParameterChanges
    {
        private readonly AgentContext _context;
        private readonly Action _left;
        private readonly bool[] _set = new bool[3];
        private readonly float[] _base = new float[3];
        private readonly IAgentNumbers[] _part = new IAgentNumbers[3];
        private readonly int[] _index = new int[3];
        private bool _listening;

        public ParameterChanges(AgentContext context)
        {
            _context = context;
            _left = RestoreAll;
        }

        /// <summary>A multiple of the parameter is set now (the body's own value is the base it multiplies).</summary>
        public bool IsSet(BodyParameter parameter) => _set[(int)parameter];

        /// <summary>The body's own value of the parameter while a multiple of it is set.</summary>
        public float BaseOf(BodyParameter parameter) => _base[(int)parameter];

        /// <summary>The parameter is times its own value until the decision in charge leaves.</summary>
        public void Set(BodyParameter parameter, IAgentNumbers part, int index, float times)
        {
            int at = (int)parameter;
            if (!_set[at])
            {
                _part[at] = part;
                _index[at] = index;
                _base[at] = part.GetNumber(index);
                _set[at] = true;
            }

            _part[at].SetNumber(_index[at], _base[at] * times);
            if (_listening || _context == null)
                return;

            _context.DecisionLeft += _left;
            _listening = true;
        }

        /// <summary>The parameter has its own value back (nothing — none of it was set).</summary>
        public void Restore(BodyParameter parameter)
        {
            int at = (int)parameter;
            if (!_set[at])
                return;

            _set[at] = false;
            _part[at]?.SetNumber(_index[at], _base[at]);
            _part[at] = null;
        }

        // The decision leaves: every parameter set in it goes back (it keeps listening: no allocation at every change
        // of decision)
        private void RestoreAll()
        {
            for (int i = 0; i < _set.Length; i++)
                Restore((BodyParameter)i);
        }
    }
}
