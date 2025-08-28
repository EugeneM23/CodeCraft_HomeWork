using TMPro;
using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class PriceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _priceText;

        [Inject] private readonly IPriceViewPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnPlanetUnlocked += Hide;
            UpdatePrice();

            if (_presenter.IsPlanetUnlocked)
                Hide();
            else
                Show();
        }

        private void OnDisable() => _presenter.OnPlanetUnlocked -= Hide;

        private void UpdatePrice() => _priceText.text = _presenter.Price;

        private void Show() => this.gameObject.SetActive(true);
        private void Hide() => this.gameObject.SetActive(false);
    }
}