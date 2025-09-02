using System;
using Modules.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class PlanetView : MonoBehaviour
    {
        [SerializeField] private Image _planetIcon;
        [SerializeField] private Image _lockIcon;
        [SerializeField] private SmartButton _button;

        [Inject] private IPlanetPresenter _presenter;

        private void OnEnable()
        {
            Show(_presenter);
        }

        public void Show(IPlanetPresenter presenter)
        {
            _presenter = presenter;

            UpdateView();

            _button.OnClick += _presenter.OnClick;
            _button.OnHold += _presenter.OnHold;
            _presenter.OnUnlocked += UpdateView;
        }

        public void Hide()
        {
            _button.OnClick -= _presenter.OnClick;
            _button.OnHold -= _presenter.OnHold;
            _presenter.OnUnlocked -= UpdateView;
        }

        private void UpdateView()
        {
            _planetIcon.sprite = _presenter.Icon;
            _lockIcon.gameObject.SetActive(!_presenter.IsUnlocked);
        }
    }
}