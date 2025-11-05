using UnityEngine;

namespace Gameplay
{
    public class ThrowItemController : ITickable, IInitializeble, IDisposable
    {
        [Inject] private readonly Character _character;
        [Inject] private readonly SpriteAnimator _animator;
        [Inject] private readonly AnimationFSM _animationFsm;

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.E))
                _animationFsm.SetState<ThrowItemState>();
        }

        public void Initialize() => _animator.OnEventRaised += ThrowItem;

        public void Dispose() => _animator.OnEventRaised -= ThrowItem;

        private void ThrowItem(EventID id)
        {
            if (id == EventID.ThrowItem)
                _character.ThrowItem();
        }
    }
}