using Atomic.Entities;
using Atomic.Presenters;
using Game.UI;
using UnityEngine;

namespace Game.DamageScreen
{
    public class DamageScreenPresenter : Presenter
    {
        [SerializeField] private HealthScreen _healthScreen;
        private IEntity _character;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;
            _character.GetDamageTakenEvent().Subscribe(OnTakeDamage);
        }

        private void OnTakeDamage(TakeDamageArgs damage)
        {
            _healthScreen.TakeDamage(damage.source.GetDamage().Value);
        }
    }
}