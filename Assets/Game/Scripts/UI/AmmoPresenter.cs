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
        private IReactiveVariable<IEntity> _weapon;
        private IEntity _currentWeapon;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayers()[ID].GetCharacter();
            _weapon = _character.GetWeapon();
            _currentWeapon = _weapon.Value;
            _weapon.Observe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            if (_currentWeapon != null)
                _currentWeapon.GetAmmo().OnStateChanged -= UpdateAmmo;

            _currentWeapon = weapon;

            if (_currentWeapon != null)
            {
                _currentWeapon.GetAmmo().OnStateChanged += UpdateAmmo;
                UpdateAmmo();
            }
            else
            {
                _statView.SetText(0.ToString());
            }
        }

        protected override void OnShow()
        {
            UpdateAmmo();
        }

        private void UpdateAmmo()
        {
            int count = _character.GetWeapon().Value.GetAmmo().GetCount();
            _statView.SetText(count.ToString());
        }

        protected override void OnHide()
        {
            _character.GetWeapon().Value.GetAmmo().OnStateChanged -= UpdateAmmo;
        }
    }
}