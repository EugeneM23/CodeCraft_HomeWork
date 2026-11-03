using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Jump input.
    ///
    /// Runs in FixedTick and on GetKeyDown. Previously it ran in LateUpdate with GetKey, so an
    /// Impulse — which is a velocity step and does not scale with deltaTime — was applied once per
    /// rendered frame for as long as the key was held: the launch height depended on frame rate and
    /// holding the key stacked impulses.
    /// </summary>
    public class CharacterJumpController : IPlayerContextInit, IPlayerContextTick, IPlayerContextFixedTick
    {
        private IEntity _character;
        private bool _jumpRequested;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
        }

        /// <summary>
        /// GetKeyDown is only reliable in Update: a fixed step can be skipped or run twice in a
        /// frame, so the press is latched here and consumed by the physics step.
        /// </summary>
        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.V))
                _jumpRequested = true;
        }

        public void FixedTick(IPlayerContext context, float deltaTime)
        {
            if (!_jumpRequested) return;
            _jumpRequested = false;

            JumpUseCase.Jump(_character);
        }
    }
}
