using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The world of a scene's agents as a component: owns an AIWorld and opens its step before the agents step (the
    /// roster's attack slots mature there). A scene does not need to place one: the first agent that asks for the
    /// group (a node that takes an attack slot) makes it and joins; an agent a game has already given a world keeps
    /// that world. A game with its own loop owns its AIWorld itself and does not use this component.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("AI Kit/AI World")]
    public sealed class AIWorldRunner : MonoBehaviour
    {
        private static AIWorldRunner _current;

        public IAIWorld World { get; } = new AIWorld();

        /// <summary>
        /// The agent joins the scene's world (made on first ask) unless it already has one. After this its
        /// context.Roster is there: the agents of the scene see each other as a group.
        /// </summary>
        public static void Join(AICharacter agent)
        {
            if (agent == null || agent.World != null)
                return;

            agent.SetWorld(Current().World);
        }

        /// <summary>The bus the scene's agents shout on and hear each other by.</summary>
        public ISignalBus Signals { get; } = new SignalBus();

        /// <summary>
        /// The agent joins the scene's signal bus unless a game has already given it one: from now on it hears what the
        /// others shout (Call For Help) and can shout itself.
        /// </summary>
        public static void Listen(AICharacter agent)
        {
            if (agent == null || agent.Context?.Signals != null)
                return;

            agent.SetSignals(Current().Signals);
        }

        private static AIWorldRunner Current()
        {
            if (_current == null)
            {
                _current = FindAnyObjectByType<AIWorldRunner>();
                if (_current == null)
                    _current = new GameObject("[AI World]").AddComponent<AIWorldRunner>();
            }

            return _current;
        }

        private void Awake()
        {
            if (_current == null)
                _current = this;
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }

        private void FixedUpdate() => World.BeginStep(Time.fixedDeltaTime);
    }
}
