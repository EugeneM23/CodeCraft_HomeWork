using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class SmashComponent
    {
        [Inject] private readonly Character _character;
        [Inject] private List<IAction> _actions;

        public interface IAction
        {
            void Invoke();
        }

        public void Smash(Vector2 power)
        {
            _character.Smash(power);
            
            foreach (var item in _actions) 
                item.Invoke();
        }
    }
}