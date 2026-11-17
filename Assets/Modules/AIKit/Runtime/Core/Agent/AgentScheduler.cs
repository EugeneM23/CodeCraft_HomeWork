using System.Collections.Generic;
using System.Diagnostics;

namespace AIKit
{
    /// <summary>
    /// Rations the decisions of a world's agents. It never steps an agent — each agent is stepped by exactly one driver
    /// (its own FixedUpdate or the game's loop component) — it only answers "may I think now?":
    /// at most MaxDecisionsPerStep agents decide per step, the rest keep executing and decide on a following step.
    /// The cap is a count, not wall-clock milliseconds, so who thinks when is the same on every machine (one seed — one
    /// trace). The stopwatch runs only around decisions and is a measurement, never a gate.
    /// Registering spreads the agents' first decisions over the think interval, so a wave of agents that woke up together
    /// does not decide on the same step forever after.
    /// Over the cap, turn by turn (handbook §27.1: the agents not reached go first on the next step): an agent that was
    /// due and had to wait queues up, and on the following steps the budget is kept for the queue, oldest first — an agent
    /// stepped early in the order cannot take every step's decision while the ones stepped after it starve. An agent
    /// that stops asking (paused, gone) leaves the queue at the next step.
    /// </summary>
    public sealed class AgentScheduler
    {
        private const float GOLDEN_FRACTION = 0.618034f;

        private readonly List<AICharacter> _agents = new();
        private readonly Stopwatch _watch = new();

        // The agents that were due and had to wait, oldest first, and whether each asked again on the current step
        private readonly List<AICharacter> _waiting = new();
        private readonly List<bool> _askedThisStep = new();

        private int _registrations;

        /// <summary>Decisions allowed per step; 0 — no cap (everyone decides when due).</summary>
        public int MaxDecisionsPerStep { get; set; }

        public int Count => _agents.Count;

        /// <summary>Agents that decided on the current step.</summary>
        public int DecisionsThisStep { get; private set; }

        /// <summary>Agents that were due but had to wait for a following step.</summary>
        public int PostponedThisStep { get; private set; }

        /// <summary>Milliseconds spent in decisions on the current step (measurement only).</summary>
        public double ThinkMillisecondsThisStep { get; private set; }

        /// <summary>Milliseconds spent in decisions since the scheduler was made (the perf test reads it).</summary>
        public double ThinkMillisecondsTotal { get; private set; }

        public bool Contains(AICharacter agent) => _agents.Contains(agent);

        /// <summary>The agent asks this scheduler before it decides; its first decision is phase-shifted.</summary>
        public void Register(AICharacter agent)
        {
            if (agent == null || _agents.Contains(agent))
                return;

            _agents.Add(agent);
            float phase = _registrations * GOLDEN_FRACTION % 1f;
            _registrations++;
            agent.DelayFirstDecision(phase);
        }

        public void Unregister(AICharacter agent)
        {
            _agents.Remove(agent);
            int waits = _waiting.IndexOf(agent);
            if (waits >= 0)
            {
                _waiting.RemoveAt(waits);
                _askedThisStep.RemoveAt(waits);
            }
        }

        public void Clear()
        {
            _agents.Clear();
            _waiting.Clear();
            _askedThisStep.Clear();
            _registrations = 0;
        }

        /// <summary>
        /// Opens a new step: the decision count starts from zero; an agent of the queue that did not ask on the step
        /// just over is not due any more (paused, gone) and leaves the queue.
        /// </summary>
        public void BeginStep()
        {
            DecisionsThisStep = 0;
            PostponedThisStep = 0;
            ThinkMillisecondsThisStep = 0d;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (!_askedThisStep[i] || _waiting[i] == null)
                {
                    _waiting.RemoveAt(i);
                    _askedThisStep.RemoveAt(i);
                }
                else
                {
                    _askedThisStep[i] = false;
                }
            }
        }

        /// <summary>
        /// True — the caller may decide now and must call EndDecision right after. An anonymous ask: first come, first
        /// served within the step's budget (the places kept for the agents queued up are not given to it).
        /// </summary>
        public bool TryBeginDecision() => TryBeginDecision(null);

        /// <summary>
        /// True — the agent may decide now and must call EndDecision right after. False — over the step's budget, or the
        /// budget left is kept for agents that have waited longer: the agent queues up and decides on a following step.
        /// </summary>
        public bool TryBeginDecision(AICharacter agent)
        {
            int queued = agent != null ? _waiting.IndexOf(agent) : -1;
            if (queued >= 0)
                _askedThisStep[queued] = true;

            if (MaxDecisionsPerStep > 0)
            {
                // The agents queued before it (all of the queue for one not in it) have the first right to what is left
                int ahead = queued >= 0 ? queued : _waiting.Count;
                if (MaxDecisionsPerStep - DecisionsThisStep <= ahead)
                {
                    if (queued < 0 && agent != null)
                    {
                        _waiting.Add(agent);
                        _askedThisStep.Add(true);
                    }

                    PostponedThisStep++;
                    return false;
                }
            }

            if (queued >= 0)
            {
                _waiting.RemoveAt(queued);
                _askedThisStep.RemoveAt(queued);
            }

            DecisionsThisStep++;
            _watch.Restart();
            return true;
        }

        public void EndDecision()
        {
            _watch.Stop();
            double milliseconds = _watch.Elapsed.TotalMilliseconds;
            ThinkMillisecondsThisStep += milliseconds;
            ThinkMillisecondsTotal += milliseconds;
        }
    }
}
