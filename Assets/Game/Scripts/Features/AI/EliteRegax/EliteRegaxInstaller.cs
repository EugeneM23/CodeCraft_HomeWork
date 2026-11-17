using AIKit;
using Atomic.Entities;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Makes a Regax the AIKit elite: its <see cref="AICharacter"/> (graph <c>Regax_Elite_Brain</c>, body
    /// <see cref="EliteRegaxBody"/>) is driven by <see cref="EliteRegaxBehaviour"/>. Listed after the Regax's core
    /// installer (its walking values, health and weapon come first).
    /// </summary>
    public sealed class EliteRegaxInstaller : MonoEntityInstaller
    {
        [SerializeField, Tooltip("The elite's AI Character (empty — the one on this object).")]
        private AICharacter _character;

        [SerializeField, Tooltip("What its Senses see with (empty — the graph's own profile).")]
        private PerceptionProfile _senses;

        /// <summary>The behaviour made by the last <see cref="Install"/>.</summary>
        public EliteRegaxBehaviour Behaviour { get; private set; }

        public override void Install(IEntity entity)
        {
            if (_character == null)
                _character = GetComponent<AICharacter>();

            entity.AddEliteRegaxTag();
            Behaviour = new EliteRegaxBehaviour(_character, _senses);
            entity.AddBehaviour(Behaviour);
        }
    }
}
