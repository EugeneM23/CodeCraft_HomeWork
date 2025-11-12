using Atomic.Elements;
using Atomic.Entities;

namespace Game.Gameplay
{
    public class CharacterFireAction : IAction
    {
        private readonly IEntity _entity;

        public CharacterFireAction(IEntity entity)
        {
            _entity = entity;
        }

        public void Invoke()
        {
            if (_entity.GetFireCondition().Invoke())
            {
                FireUseCase.Fire(_entity.GetWeapon());
                _entity.GetFireEvent().Invoke();
            }
        }
    } 
}