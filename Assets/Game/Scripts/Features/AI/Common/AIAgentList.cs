using System.Collections.Generic;
using AIKit;
using Unity.Profiling;

namespace Game.AI
{
    /// <summary>
    /// The agents of a level's AIKit world, in the order they joined. <see cref="Step"/> opens the world's step (its
    /// decision budget) and then steps every agent once — the only driver of their characters (Manual simulation).
    /// </summary>
    public sealed class AIAgentList
    {
        private static readonly ProfilerMarker StepMarker = new("AI.Step");

        private readonly IAIWorld _world;
        private readonly List<IAIAgent> _agents = new(16);

        public AIAgentList(IAIWorld world) => _world = world;

        public IAIWorld World => _world;

        public int Count => _agents.Count;

        public IAIAgent this[int index] => _agents[index];

        /// <summary>Joins the agent to the world (its decisions are budgeted from now) and to the step.</summary>
        public void Add(IAIAgent agent)
        {
            if (agent == null || _agents.Contains(agent))
                return;
            _agents.Add(agent);
            agent.Character?.SetWorld(_world);
        }

        public void Remove(IAIAgent agent)
        {
            if (agent == null || !_agents.Remove(agent))
                return;
            if (agent.Character != null && agent.Character.World == _world)
                agent.Character.SetWorld(null);
        }

        /// <summary>The world's step, then every agent's. An agent that leaves meanwhile (it died) is not stepped again.</summary>
        public void Step(float dt)
        {
            using (StepMarker.Auto())
            {
                _world?.BeginStep(dt);
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i].Step(dt);
            }
        }
    }
}
