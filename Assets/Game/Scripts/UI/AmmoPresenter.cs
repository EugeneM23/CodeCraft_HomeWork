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
        private IEntity _currentWeapon;

        protected override void OnInit()
        {
            _character = GameContext.Instance.GetPlayerCharacter().Value;
            _weapon = _character.GetWeapon();
            _currentWeapon = _weapon.Value;
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
            _weapon.Value.GetAmmo().OnStateChanged -= UpdateAmmo;
        }
    }
}