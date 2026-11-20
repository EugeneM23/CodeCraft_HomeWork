using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// Makes an Atomic entity an AIKit agent: its <see cref="AICharacter"/> (on this object, unless given) becomes the
    /// entity's <c>AgentCharacter</c>, the entity gets the agent's inputs and outputs (<see cref="AIAgentAPI"/>) and the
    /// <see cref="AIAgentBehaviour"/> that binds the two. List it after the installers of the entity's walker: values a
    /// game already put under <c>AgentMove</c>, <c>AgentFace</c>, <c>AgentSpeed</c> or <c>AIHitTaken</c> (its own
    /// walker's variables) are kept, the missing ones are made.
    /// <para>The character's body is usually an <see cref="EntityBody"/> (its walk goes into the entity); any other body
    /// keeps driving its own way.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIAgentInstaller : MonoEntityInstaller
    {
        [SerializeField, Tooltip("The agent's AI Character (empty — the one on this object).")]
        private AICharacter _character;

        [SerializeField, Tooltip("The entity that owns the AI world (empty — the first one there is; none — the entity steps " +
                                 "the agent itself).")]
        private MonoEntity _world;

        /// <summary>The behaviour made by the last <see cref="Install"/>: bind graph variables to entity values with it.</summary>
        public AIAgentBehaviour Behaviour { get; private set; }

        public AICharacter Character => _character != null ? _character : _character = GetComponent<AICharacter>();

        /// <summary>The world's entity for the next <see cref="Install"/> (a level made in code).</summary>
        public void SetWorld(MonoEntity world) => _world = world;

        public override void Install(IEntity entity)
        {
            AICharacter character = Character;
            if (character == null)
            {
                Debug.LogError($"{name}: AI Agent Installer has no AI Character.", this);
                return;
            }

            Behaviour = Install(entity, character, _world);
        }

        /// <summary>The agent's values and behaviour on <paramref name="entity"/> (an entity made in code, a game's installer).</summary>
        public static AIAgentBehaviour Install(IEntity entity, AICharacter character, IEntity world = null)
        {
            entity.AddAIAgentTag();
            entity.AddAgentCharacter(character);
            if (!entity.HasAgentMove())
                entity.AddAgentMove(new ReactiveVariable<Vector3>());
            if (!entity.HasAgentFace())
                entity.AddAgentFace(new ReactiveVariable<Vector3>());
            if (!entity.HasAIHitTaken())
                entity.AddAIHitTaken(new Event<HitInfo>());
            entity.AddAgentDecision(new ReactiveVariable<string>(string.Empty));
            entity.AddAgentTarget(new ReactiveVariable<Transform>());
            entity.AddAgentThinkRequest(new Request());
            entity.AddAgentHitRequest(new Request<HitInfo>());

            var behaviour = new AIAgentBehaviour(character, world);
            entity.AddBehaviour(behaviour);
            entity.AddBehaviour(new AIAgentGizmos());
            return behaviour;
        }
    }
}
