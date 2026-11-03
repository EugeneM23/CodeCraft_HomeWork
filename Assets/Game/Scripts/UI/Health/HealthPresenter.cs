using System;
using Atomic.Elements;
using Atomic.Entities;
using Game.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    public class HealthPresenter : Presenter
    {
        [SerializeField] private StatView _statView;
        [SerializeField] private PlayerID ID;

        private IEntity _character;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;
        }


        protected override void OnShow()
        {
            _character.GetHealth().OnHealthChanged += OnHealthChanged;
            _statView.SetText(_character.GetHealth().GetCurrent().ToString());
        }

        protected override void OnHide()
        {
            // OnHide also runs while the scene is being torn down, and MonoEntity.OnDestroy now
            // clears its values (the old SceneEntity only disposed them), so the lookup can fail.
            if (_character != null && _character.TryGetHealth(out var health))
                health.OnHealthChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int health)
        {
            _statView.SetText(health.ToString());
        }
    }
}