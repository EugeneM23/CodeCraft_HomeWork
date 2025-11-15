using Atomic.Elements;
using Atomic.Entities;
using Atomic.Presenters;
using Game.UI;
using UnityEngine;

namespace Game
{
    public class AmmoPresenter : Presenter 
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
            _character.GetWeapon().GetAmmo().Observe(OnAmmoChanged);
        }

        protected override void OnHide()
        {
            _character.GetWeapon().GetAmmo().Unsubscribe(OnAmmoChanged);
        }

        private void OnAmmoChanged(int ammo)
        {
            _statView.SetText(ammo.ToString());
        }
    }
}