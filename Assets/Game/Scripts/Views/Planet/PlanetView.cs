using Modules.UI;
using TMPro;
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

        [Inject] private readonly IPlanetViewPresenter _presenter;

        private void OnEnable()
        {
            UpdateView();

            _button.OnClick += OnClick;
            _button.OnHold += ShowPopup;
            _presenter.OnUnlocked += HandleUnlocked;
        }

        private void OnDisable()
        {
            _button.OnHold -= OnClick;
            _button.OnHold -= ShowPopup;
            _presenter.OnUnlocked -= HandleUnlocked;
        }

        private void ShowPopup() => _presenter.ShowPlanetPopup();

        private void UpdateView()
        {
            _planetIcon.sprite = _presenter.Icon;
            _lockIcon.gameObject.SetActive(!_presenter.IsUnlocked);
        }

        private void OnClick()
        {
            if (!_presenter.IsUnlocked)
                _presenter.UnlockPlanet();
        }

        private void HandleUnlocked() => UpdateView();
    }
}