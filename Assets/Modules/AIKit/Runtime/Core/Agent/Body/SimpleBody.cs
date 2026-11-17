using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A body without physics: moves the transform where the AI holds the stick. For prototypes, crowd units and
    /// tests — the behavior, the graph and the navigation are exactly the ones a real character gets.
    /// </summary>
    [Serializable]
    public sealed class SimpleBody : AgentBody
    {
        [Tooltip("Top speed, units per second")]
        [Min(0f)]
        public float Speed = 4f;

        [Tooltip("On — movement stays in the XY plane (2D); off — full 3D")]
        public bool PlanarXY;

        [Min(0f)]
        public float Radius = 0.3f;

        [Min(0f)]
        public float Height = 1.6f;

        [NonSerialized] private TransformMover _mover;

        public override void Attach(Transform owner) => _mover = new TransformMover(owner, this);

        public override IAgentActuator Actuator => _mover;
    }
}
