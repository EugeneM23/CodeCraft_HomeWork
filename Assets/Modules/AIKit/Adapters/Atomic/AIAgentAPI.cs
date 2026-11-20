using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// Entity keys of an AIKit agent and of the AI world it is stepped by (Docs/AI/AIKit_Atomic.md). The extensions
    /// (<c>GetAgentCharacter()</c>, <c>AddAgentMove(...)</c>...) come from the Atomic source generator.
    /// <para>The agent's entity is the character's own entity — there is no separate "brain" entity. What goes in: the
    /// game writes values and requests (a hit, "think now", facts bound to graph variables). What comes out: the
    /// actuator writes the walk into <see cref="AgentMove"/> / <see cref="AgentFace"/> (a game puts its own walker's
    /// variables under these keys), and the agent shows its decision and target as values views can watch.</para>
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class AIAgentAPI
    {
        #region Agent

        /// <summary>An entity driven by an AIKit agent.</summary>
        public static readonly TagKey<IEntity> AIAgent = new(nameof(AIAgent));

        /// <summary>The agent's character: its graph, blackboard and context.</summary>
        public static readonly ValueKey<IEntity, AICharacter> AgentCharacter = new(nameof(AgentCharacter));

        /// <summary>Where the agent walks this step (flat, at most unit length; zero — it stands). Written by its actuator.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> AgentMove = new(nameof(AgentMove));

        /// <summary>Where the agent faces this step (flat unit vector; zero — no turn). Written by its actuator.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> AgentFace = new(nameof(AgentFace));

        /// <summary>The walking speed the agent plans with, metres per second (optional; a game shares its own).</summary>
        public static readonly ValueKey<IEntity, IValue<float>> AgentSpeed = new(nameof(AgentSpeed));

        /// <summary>The agent's current decision or block by name (empty — none). Written on every change of state.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<string>> AgentDecision = new(nameof(AgentDecision));

        /// <summary>What the agent is after (null — nothing). Written after each step when it changes.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<Transform>> AgentTarget = new(nameof(AgentTarget));

        /// <summary>"Think on the next step" (something worth a decision happened); consumed by the agent's step.</summary>
        public static readonly ValueKey<IEntity, IRequest> AgentThinkRequest = new(nameof(AgentThinkRequest));

        /// <summary>The game's damage reached the agent: it takes the hit on its next step (memory, flinch, a think).</summary>
        public static readonly ValueKey<IEntity, IRequest<HitInfo>> AgentHitRequest = new(nameof(AgentHitRequest));

        /// <summary>A blow of AIKit content (a weapon, an ability) landed on this entity: the game turns it into damage.</summary>
        public static readonly ValueKey<IEntity, IEvent<HitInfo>> AIHitTaken = new(nameof(AIHitTaken));

        #endregion

        #region World

        /// <summary>The entity that steps AIKit agents (one per level): their world and their order.</summary>
        public static readonly TagKey<IEntity> AIWorldOwner = new(nameof(AIWorldOwner));

        /// <summary>The agents the world steps, in the order they joined, and their AIKit world.</summary>
        public static readonly ValueKey<IEntity, AIAgentList> WorldAgentList = new(nameof(WorldAgentList));

        #endregion
    }
}
