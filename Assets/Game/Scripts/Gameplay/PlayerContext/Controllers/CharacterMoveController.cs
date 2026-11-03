using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Samples movement input and publishes it as a direction on the character.
    /// The on-screen joysticks were removed: this is keyboard only now.
    ///
    /// Character comes from the reactive variable instead of being snapshotted at Init, so a
    /// respawn retargets the input rather than leaving it driving the previous entity.
    /// </summary>
    public class CharacterMoveController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;
        private IReactiveVariable<Vector3> _moveDirection;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character) =>
            _moveDirection = character?.GetMoveDirection();

        private void Move()
        {
            if (_moveDirection == null) return;

            Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            _moveDirection.Value = direction;
        }

        public void LateTick(IPlayerContext context, float deltaTime) => Move();
    }
}
