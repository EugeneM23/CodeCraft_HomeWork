using System;
using Atomic.Elements;
using Atomic.Entities;
using Atomic.Presenters;
using Game.UI;
using UnityEngine;

namespace Game
{
    public class HealthPresenter : Presenter
    {
        [SerializeField] private StatView _statView;
        [SerializeField] private PlayerID ID;

        private IEntity _character;
        private IReactiveVariable<IEntity> _weapon;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayerCharacter().Value;
            _weapon = _character.GetWeapon();
        }

        

        protected override void OnShow()
        {
            _character.GetHealth().OnHealthChanged += OnHealthChanged;
            _statView.SetText(_character.GetHealth().GetCurrent().ToString());
        }

        protected override void OnHide()
        {
            _character.GetHealth().OnHealthChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int health)
        {
            _statView.SetText(health.ToString());
        }
    }
}