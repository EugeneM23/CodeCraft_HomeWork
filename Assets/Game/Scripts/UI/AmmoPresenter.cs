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
            
            int count = _character.GetWeapon().Value.GetAmmo().GetCount();
            _statView.SetText(0.ToString());
        }

        protected override void OnShow()
        {
            int count = _character.GetWeapon().Value.GetAmmo().GetCount();
            _statView.SetText(count.ToString());
        }

        protected override void OnHide()
        {
        }

        private void OnAmmoChanged(int ammo)
        {
            _statView.SetText(ammo.ToString());
        }
    }
}