using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class ShowInteractUIBehaviour : IEntityInit
    {
        private IReactiveVariable<IEntity> target;
        private IEntity _current;

        public void Init(in IEntity entity)
        {
            target = entity.GetTargetInteractable();

            target.Subscribe(OnInteract);
        }

        private void OnInteract(IEntity obj)
        {
            if (obj == null)
            {
                InteractUseCase.ShowUI(_current, false);
                _current = null;
            }

            if (obj != null && obj != _current)
            {
                if (_current != null) InteractUseCase.ShowUI(_current, false);

                _current = obj;
                InteractUseCase.ShowUI(obj, true);
            }
        }
    }
}