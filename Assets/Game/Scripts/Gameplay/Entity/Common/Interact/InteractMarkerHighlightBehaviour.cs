using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class InteractMarkerHighlightBehaviour : IEntityInit, IEntityDispose
    {
        private IReactiveVariable<IEntity> _character;
        private IEntity _currentItem;
        private IReactiveVariable<IEntity> _targetInteractable;

        public void Init(in IEntity entity)
        {
            _character = GameContext.Instance.GetPlayerContext().GetCharacter();
            _targetInteractable = _character.Value.GetTargetInteractable();
            _targetInteractable.Subscribe(OnTargetChanged);
        }

        public void Dispose(in IEntity entity)
        {
            _targetInteractable.Unsubscribe(OnTargetChanged);
        }

        private void OnTargetChanged(IEntity item)
        {
            if (_currentItem != null && _currentItem.TryGetHighlight(out var currentEffect))
            {
                currentEffect.enabled = false;
            }

            if (item != null)
            {
                if (item.TryGetHighlight(out var highlightEffect))
                {
                    highlightEffect.enabled = true;
                    highlightEffect.TargetFX();
                }

                _currentItem = item;
            }
            else
            {
                _currentItem = null;
            }
        }
    }
}