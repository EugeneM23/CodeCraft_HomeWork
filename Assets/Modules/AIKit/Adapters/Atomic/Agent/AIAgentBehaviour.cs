using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// An AIKit agent on its Atomic entity: the entity owns the agent's life and its inputs and outputs, the agent owns
    /// its memory and decisions (Docs/AI/AIKit_Atomic.md).
    /// <list type="bullet">
    /// <item><b>Life.</b> At <see cref="Init"/> the character is bound to the entity (its <see cref="EntityBody"/>, if it
    /// has one, gets the entity's walk values) and joins the world's agents (<c>WorldAgentList</c> of the world entity:
    /// Manual simulation, the world steps it in order and rations its decisions). Without a world the entity's own fixed tick
    /// steps it. At <see cref="Dispose"/> (death, the pool) it leaves the world, its abilities are broken
    /// off and the body stands.</item>
    /// <item><b>In.</b> Each step, before the agent's: the values bound to graph variables are copied in when they
    /// changed (<see cref="Bind{T}"/>), <c>AgentThinkRequest</c> makes it think now, <c>AgentHitRequest</c> is taken as
    /// a hit (the game's damage). Then <c>AICharacter.Step</c>.</item>
    /// <item><b>Out.</b> <c>AgentDecision</c> follows the graph's state (on its change), <c>AgentTarget</c> what the agent
    /// is after (after each step, when it changed); the walk is the body's (<c>AgentMove</c> / <c>AgentFace</c>).</item>
    /// </list>
    /// Values are taken at <see cref="Init"/>, never looked up in a step; nothing allocates per step.
    /// </summary>
    public sealed class AIAgentBehaviour : IEntityInit, IEntityFixedTick, IEntityDispose, IAIAgent
    {
        private readonly AICharacter _character;
        private readonly List<IVariableBinding> _bindings = new(4);

        private IEntity _world;
        private AIAgentList _agents;
        private IRequest _think;
        private IRequest<HitInfo> _hit;
        private IReactiveVariable<string> _decision;
        private IReactiveVariable<Transform> _target;
        private bool _bound;

        /// <param name="character">The agent's character (its graph, blackboard, body).</param>
        /// <param name="world">The entity that owns the world (<c>AIWorldOwner</c>); null — the first one registered, or
        /// none: the agent drives itself.</param>
        public AIAgentBehaviour(AICharacter character, IEntity world = null)
        {
            _character = character != null ? character : throw new ArgumentNullException(nameof(character));
            _world = world;
        }

        public AICharacter Character => _character;

        /// <summary>The world's agents it joined; null — it drives itself.</summary>
        public AIAgentList Agents => _agents;

        /// <summary>
        /// The graph variable <paramref name="variable"/> follows <paramref name="source"/>: copied into the agent's
        /// blackboard before each step in which it changed (and before the first). How a game hands the graph its facts
        /// (a blast nearby, an order) through the entity instead of writing into the blackboard.
        /// </summary>
        public void Bind<T>(IValue<T> source, string variable)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(variable))
                throw new ArgumentException("A graph variable name is needed", nameof(variable));
            _bindings.Add(new VariableBinding<T>(source, variable));
        }

        public void Init(IEntity entity)
        {
            _think = entity.GetAgentThinkRequest();
            _hit = entity.GetAgentHitRequest();
            _decision = entity.GetAgentDecision();
            _target = entity.GetAgentTarget();

            _world ??= FindWorld();
            if (_world == null || !_world.TryGetWorldAgentList(out _agents))
                _agents = null;                            // no world: the entity's own fixed tick steps it
            _character.Simulation = AICharacter.SimulationMode.Manual;

            _character.Initialize();
            if (_character.Body is EntityBody body)
                body.Bind(entity);
            _character.StateChanged += OnStateChanged;
            _decision.Value = NameOf(_character.State);
            _agents?.Add(this);
            _bound = true;
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_agents == null)
                Step(deltaTime);
        }

        public void Dispose(IEntity entity)
        {
            if (!_bound)
                return;

            _bound = false;
            _character.StateChanged -= OnStateChanged;
            _agents?.Remove(this);
            _character.Abilities.Cancel();
            _character.Context?.Actuator?.Stop();
        }

        /// <summary>One step of the agent (the world calls it; without a world, the entity's fixed tick).</summary>
        public void Step(float dt)
        {
            if (!_bound)
                return;

            PushInputs();
            _character.Step(dt);
            PullOutputs();
        }

        /// <summary>The inputs of a step: bound variables, "think now", a hit. Called by <see cref="Step"/>.</summary>
        internal void PushInputs()
        {
            Blackboard board = _character.Blackboard;
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Push(board);

            if (_think.Consume())
                _character.RequestThink();
            if (_hit.Consume(out HitInfo hit))
                _character.TakeHit(hit);
        }

        private void PullOutputs()
        {
            AgentContext context = _character.Context;
            Transform target = context == null ? null : context.BrainTarget != null ? context.BrainTarget : context.Target;
            if (_target.Value != target)
                _target.Value = target;
        }

        private void OnStateChanged(AgentState from, AgentState to)
        {
            string name = NameOf(to);
            if (_decision.Value != name)
                _decision.Value = name;
        }

        private static string NameOf(AgentState state) => state != null ? state.DisplayName ?? string.Empty : string.Empty;

        // Looked up once, at Init: the first entity that owns a world
        private static IEntity FindWorld()
        {
            foreach (IEntity entity in EntityRegistry.Instance)
                if (entity != null && entity.HasAIWorldOwnerTag())
                    return entity;
            return null;
        }

        private interface IVariableBinding
        {
            void Push(Blackboard board);
        }

        private sealed class VariableBinding<T> : IVariableBinding
        {
            private readonly IValue<T> _source;
            private readonly string _name;
            private BlackboardKey<T> _key;
            private int _layout = -1;
            private bool _pushed;
            private T _last;

            public VariableBinding(IValue<T> source, string name)
            {
                _source = source;
                _name = name;
            }

            public void Push(Blackboard board)
            {
                T value = _source.Value;
                bool relaid = _layout != board.Layout;
                if (_pushed && !relaid && EqualityComparer<T>.Default.Equals(value, _last))
                    return;

                if (relaid)
                {
                    _key = board.Declare<T>(_name);       // the slot of the name (the graph may have declared it already)
                    _layout = board.Layout;
                }

                board.Set(_key, value);
                _last = value;
                _pushed = true;
            }
        }
    }
}
