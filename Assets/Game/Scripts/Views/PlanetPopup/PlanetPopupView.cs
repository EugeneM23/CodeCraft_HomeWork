using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class PlanetPopupView : MonoBehaviour
    {
        [Header("Planet Info")] [SerializeField]
        private Image _icon;

        [SerializeField] private TMP_Text _population;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _income;

        [Header("Upgrade")] [SerializeField] private TMP_Text _upgradePrice;
        [SerializeField] private Button _upgradeButton;

        [Header("Controls")] [SerializeField] private Button _closeButton;

        private IPlanetPopupPresenter  _presenter;

        [Inject]
        private void Construct(IPlanetPopupPresenter presenter) => _presenter = presenter;

        public void  Show(bool isActive)
        {
            _presenter.OnMoneyChanged += UpdatePriceButton;
            _presenter.OnUpgraded += UpdatePriceButton;
            _presenter.OnUpgraded += UpdatePlanetInformation;

             _closeButton.onClick.AddListener(_presenter.OnCloseClicked);
            _upgradeButton.onClick.AddListener(_presenter.OnUpgradeClicked);

            gameObject.SetActive(isActive);

            UpdatePlanetInformation();
            UpdatePriceButton();
        }

        private void Hide()
        {
            _presenter.OnMoneyChanged -= UpdatePriceButton;
            _presenter.OnUpgraded -= UpdatePriceButton;
            _presenter.OnUpgraded -= UpdatePlanetInformation;

            _closeButton.onClick.RemoveListener(_presenter.OnCloseClicked);
            _upgradeButton.onClick.RemoveListener(_presenter.OnUpgradeClicked);

            gameObject.SetActive(false);
        }

        private void UpdatePriceButton()
        {
            _upgradePrice.text = _presenter.UpgradePrice;
            _upgradeButton.interactable = _presenter.CanUpgrade;
        }

        private void UpdatePlanetInformation()
        {
            _icon.sprite = _presenter.Icon;
            _population.text = _presenter.Population;
            _level.text = _presenter.Level;
            _income.text = _presenter.Income;
        }
    }
}