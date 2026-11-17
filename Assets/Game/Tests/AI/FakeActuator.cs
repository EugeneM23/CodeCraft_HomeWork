using AIKit;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>A gamepad that only remembers: where the body is (set by the test) and the last stick it was given.</summary>
    internal sealed class FakeActuator : IAgentActuator
    {
        public Vector3 Position;
        public Vector3 LastMove;
        public int Moves;

        public AgentBodyInfo Body => new() { Position = Position, Up = Vector3.up, Forward = Vector3.forward, MaxSpeed = 5f };

        public void Move(Vector3 direction, float speedScale = 1f)
        {
            LastMove = direction * speedScale;
            Moves++;
        }

        public void SetVertical(float axis)
        {
        }

        public void Face(Vector3 direction)
        {
        }

        public void Jump(float holdTime)
        {
        }

        public float HoldTimeForHeight(float rise) => 0f;

        public void Crouch(bool crouch)
        {
        }

        public void DropThrough()
        {
        }

        public void Stop() => LastMove = Vector3.zero;

        public void Apply(float dt)
        {
        }
    }
}
