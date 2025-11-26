using Atomic.Elements;
using Atomic.Entities;
using Atomic.Presenters;
using Game.UI;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class AmmoPresenter : Presenter
    {
        [SerializeField] private GameObject _infinityIcon;
        [SerializeField] private StatView _statView;
        [SerializeField] private PlayerID ID;

        private IEntity _character;
        private IReactiveVariable<IEntity> _weapon;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;
            _weapon = _character.GetWeapon();
            _weapon.Observe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            if (_weapon.Value != null && _weapon.Value.TryGetAmmo(out Ammo ammo))
            {
                ammo.OnStateChanged += UpdateAmmo;
                _statView.gameObject.SetActive(true);
                _infinityIcon.SetActive(false);
                UpdateAmmo();
            }
            else
            {
                _infinityIcon.SetActive(true);
                _statView.gameObject.SetActive(false);
            }
        }

        private void UpdateAmmo()
        {
            int count = _weapon.Value.GetAmmo().GetCount();
            _statView.SetText(count.ToString());
        }

        protected override void OnHide()
        {
            if (_weapon.Value.TryGetAmmo(out var ammo)) 
                ammo.OnStateChanged -= UpdateAmmo;
        }
    }
}