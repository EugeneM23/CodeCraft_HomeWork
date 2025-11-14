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
            if (_weapon.GetFireCondition().Invoke() && _weapon.GetAmmo().Value > 0)
            {
                FireUseCase.Fire(_weapon, _gameContext);
                _weapon.GetAmmo().Value--;
                Debug.Log(_weapon.GetAmmo().Value);
                _weapon.GetFireEvent().Invoke();
            }
        }
    }
}