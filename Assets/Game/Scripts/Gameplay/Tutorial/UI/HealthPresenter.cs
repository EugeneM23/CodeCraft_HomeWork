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

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayers()[ID].GetCharacter();
        }

        protected override void OnShow()
        {
            _character.GetHealth().Observe(OnHealthChanged);
        }

        protected override void  OnHide()
        {
            _character.GetHealth().Unsubscribe(OnHealthChanged);
        }

        private void OnHealthChanged(int health)
        {
            _statView.SetText(health.ToString());
        }
    }
}