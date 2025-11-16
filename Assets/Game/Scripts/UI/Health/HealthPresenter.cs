using Atomic.Entities;
using Atomic.Presenters;
using Game.UI;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class HealthPresenter : Presenter
    {
        [SerializeField] private StatView _statView;
        [SerializeField] private PlayerID ID;
        [SerializeField] private SceneEntity _sceneEntity;

        private IEntity _character;

        protected override void OnInit()
        {
            /*
            _character = GameContext.Instance.GetPlayers()[ID].GetCharacter();
            Debug.Log(_character.GetHealth() == null);
        */
        }

        protected override void OnShow()
        {
            _sceneEntity.GetHealth().OnHealthChanged += OnHealthChanged;
        }

        protected override void OnHide()
        {
            _sceneEntity.GetHealth().OnHealthChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int health)
        {
            _statView.SetText(health.ToString());
        }
    }
}