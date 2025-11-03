using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class CharacterDeathObserver : IInitializeble, IDisposable
    {
        private HealthComponent _healthComponent;
        private CharacterController2D _character;

        private List<IAction> _actions;

        public interface IAction
        {
            void Invoke(CharacterController2D character);
        }

        [Inject]
        public void Construct(HealthComponent healthComponent, CharacterController2D character, List<IAction> actions)
        {
            _actions = actions;
            _healthComponent = healthComponent;
            _character = character;
        }

        public void Initialize() => _healthComponent.OnDeath += Death;

        public void Dispose() => _healthComponent.OnDeath -= Death;

        private void Death()
        {
            foreach (var item in _actions)
                item.Invoke(_character);

            GameObject.Destroy(_character.gameObject);
        }
    }
}