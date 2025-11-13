using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterFireAction : IAction
    {
        private readonly IEntity _weapon;
        private readonly IGameContext _gameContext;

        public CharacterFireAction(IEntity weapon, in IGameContext gameContext)
        {
            _gameContext = gameContext;
            _weapon = weapon;
        }

        public void Invoke()
        {
            if (_weapon.GetFireCondition().Invoke())
            {
                FireUseCase.Fire(_weapon, _gameContext);
                _weapon.GetFireEvent().Invoke();
            }
        }
    }
}