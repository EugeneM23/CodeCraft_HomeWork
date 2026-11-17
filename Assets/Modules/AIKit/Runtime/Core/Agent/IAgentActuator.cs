using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The only border between the AI and a body: a "virtual gamepad". The graph and the navigator press the buttons,
    /// the adapter applies them to whatever moves the character (CharacterBody, NavMeshAgent, a transform).
    /// Presses are one-shot — Apply(dt) flushes them once per step, after the graph has ticked.
    /// Nothing above this interface knows the words Rigidbody, NavMeshAgent or Animator.
    /// </summary>
    public interface IAgentActuator
    {
        /// <summary>What the body can do and where it is now (read-only snapshot).</summary>
        AgentBodyInfo Body { get; }

        /// <summary>Hold the stick toward a direction (a 2D platformer adapter reads the horizontal part only).</summary>
        void Move(Vector3 direction, float speedScale = 1f);

        /// <summary>
        /// The vertical stick, -1..1, for this step: up / down a ladder, swimming, climbing (a body that cannot just
        /// ignores it). Flying uses Move with a vertical part instead.
        /// </summary>
        void SetVertical(float axis);

        /// <summary>Turn to face a direction without moving.</summary>
        void Face(Vector3 direction);

        /// <summary>Press jump and keep it held this long (the hold time sets the height); 0 — a full jump.</summary>
        void Jump(float holdTime);

        /// <summary>
        /// How long to hold jump for the feet to rise this much (the navigator jumps exactly as high as the edge needs);
        /// a rise above the full jump gives the full hold.
        /// </summary>
        float HoldTimeForHeight(float rise);

        void Crouch(bool crouch);

        /// <summary>Down + jump: fall through the one-way platform underfoot.</summary>
        void DropThrough();

        /// <summary>Everything released: no movement, nothing held.</summary>
        void Stop();

        /// <summary>Flushes the buffered presses to the body; called once per step by the agent.</summary>
        void Apply(float dt);
    }
}
