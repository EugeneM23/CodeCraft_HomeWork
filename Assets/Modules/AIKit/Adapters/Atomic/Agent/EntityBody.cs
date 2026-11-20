using System;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// A body for an <see cref="AICharacter"/> on an Atomic entity (picked in its Body section): the gamepad is an
    /// <see cref="EntityActuator"/> — the agent's walk goes into the entity's <c>AgentMove</c> / <c>AgentFace</c> and
    /// the entity's own behaviours move it. The way is walked straight (AIKit's <see cref="DirectNavigator"/>) unless a
    /// game gives a navigator. The entity comes later than the body (<see cref="AIAgentInstaller"/> binds it).
    /// </summary>
    [Serializable]
    public sealed class EntityBody : AgentBody
    {
        [Tooltip("Body radius, metres (spacing, reach)")]
        [Min(0f)] public float Radius = 0.5f;

        [Tooltip("Body height, metres (eyes, aim)")]
        [Min(0f)] public float Height = 2f;

        [Tooltip("Highest step it walks up, metres")]
        [Min(0f)] public float StepHeight = 0.4f;

        [Tooltip("Speed it plans with, metres per second, when the entity has no AgentSpeed")]
        [Min(0f)] public float Speed = 3f;

        [NonSerialized] private EntityActuator _actuator;

        public override void Attach(Transform owner) => _actuator = new EntityActuator(owner, this);

        public override IAgentActuator Actuator => _actuator;

        /// <summary>Gives the gamepad the entity's walk values (the installer calls it).</summary>
        public void Bind(IEntity entity) => _actuator?.Bind(entity);
    }
}
