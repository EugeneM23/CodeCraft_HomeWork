using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class DashComponent
    {
        [Inject] private readonly Character _character;
        [Inject] private readonly List<IAction> _actions;

        private readonly int _power;

        public DashComponent(int power = 50) => _power = power;

        public interface IAction
        {
            void Invoke();
        }

        public void Dash(Vector2 direction)
        {
            foreach (var item in _actions)
                item.Invoke();

            _character.Dash(direction * _power);
        }
    }
}