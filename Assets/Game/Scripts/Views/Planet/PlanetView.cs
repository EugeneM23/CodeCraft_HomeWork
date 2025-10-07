using Modules.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public class PlanetView : MonoBehaviour
    {
        [Header("Locked State")] [SerializeField]
        private GameObject _price;

        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private GameObject _lockIcon;

        [Header("Planet")] [SerializeField] private Image _planetIcon;
        [SerializeField] private SmartButton _button;

        [Header("Income")] [SerializeField] private GameObject _income;
        [SerializeField] private Image _progressImage;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private CoinMoveController _coin;

        private IPlanetPresenter _presenter;

        public void Construct(IPlanetPresenter presenter)
        {
            _presenter = presenter;
            _coin.Construct(presenter);
        }

        public void Show()
        {
            _presenter.OnPlanetUnlocked += UpdatePlanetState;
            _presenter.OnPlanetChanged += UpdatePlanetState;
            _presenter.OnIncomeProgressUpdated += UpdateIncomeProgress;
            _presenter.OnIncomeReady += UpdateIncomeState;
            _button.OnClick += _presenter.OnClick;
            _button.OnHold += _presenter.OnHold;

            UpdatePlanetState();
        }

        public void Hide()
        {
            _presenter.OnPlanetUnlocked -= UpdatePlanetState;
            _presenter.OnPlanetChanged -= UpdatePlanetState;
            _presenter.OnIncomeProgressUpdated -= UpdateIncomeProgress;
            _presenter.OnIncomeReady -= UpdateIncomeState;

            _button.OnClick -= _presenter.OnClick;
            _button.OnHold -= _presenter.OnHold;
        }

        private void UpdatePlanetState()
        {
            bool isUnlocked = _presenter.IsUnlocked;

            _planetIcon.sprite = _presenter.Icon;
            _priceText.text = _presenter.Price;

            _lockIcon.SetActive(!isUnlocked);
            _price.SetActive(!isUnlocked);
            _coin.gameObject.SetActive(isUnlocked && _presenter.IsIncomeReady);
            _income.SetActive(isUnlocked && !_presenter.IsIncomeReady);
        }

        private void UpdateIncomeState(bool isReady)
        {
            _coin.gameObject.SetActive(isReady);
            _income.SetActive(!isReady);
        }

        private void UpdateIncomeProgress(float progress, string timeText)
        {
            _progressImage.fillAmount = progress;
            _progressText.text = timeText;
        }
    }
}