using Modules.UI;
using PlasticPipe.PlasticProtocol.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

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
        [SerializeField] private GameObject _coin;

        [Inject] private IPlanetPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnPlanetUnlocked += UpdatePlanetState;
            _presenter.OnPlanetChanged += UpdatePlanetState;
            _presenter.OnIncomeProgressUpdated += UpdateIncomeProgress;
            _presenter.OnIncomeReady += UpdateIncomeReadyState;

            _button.OnClick += _presenter.OnClick;
            _button.OnHold += _presenter.OnHold;

            UpdatePlanetState();
        }

        private void OnDisable()
        {
            _presenter.OnPlanetUnlocked -= UpdatePlanetState;
            _presenter.OnPlanetChanged -= UpdatePlanetState;
            _presenter.OnIncomeProgressUpdated -= UpdateIncomeProgress;
            _presenter.OnIncomeReady -= UpdateIncomeReadyState;

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
            _coin.SetActive(isUnlocked && _presenter.IsIncomeReady);
            _income.SetActive(isUnlocked && !_presenter.IsIncomeReady);
        }

        private void UpdateIncomeReadyState(bool isReady)
        {
            _coin.SetActive(isReady);
            _income.SetActive(!isReady);
        }

        private void UpdateIncomeProgress(float progress, string timeText)
        {
            _progressImage.fillAmount = progress;
            _progressText.text = timeText;
        }
    }
}