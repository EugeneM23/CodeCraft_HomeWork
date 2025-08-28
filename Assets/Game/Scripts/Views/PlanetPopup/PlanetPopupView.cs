using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class PlanetPopupView : MonoBehaviour
    {
        [Header("Planet Info")] 
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _population;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _income;

        [Header("Upgrade")] 
        [SerializeField] private TMP_Text _upgradePrice;
        [SerializeField] private Button _upgradeButton;

        [Header("Controls")] 
        [SerializeField] private Button _closeButton;

        private IPlanetPopupViewPresenter _presenter;

        [Inject]
        private void Construct(IPlanetPopupViewPresenter presenter)
        {
            _presenter = presenter;
            _presenter.OnShow += Show;
        }

        private void OnDestroy() => _presenter.OnShow -= Show;

        private void OnEnable()
        {
            _presenter.OnMoneyChanged += UpdatePriceButton;
            _presenter.OnUpgraded += UpdatePriceButton;
            _presenter.OnUpgraded += UpdatePlanetInformation;

            _closeButton.onClick.AddListener(Hide);
            _upgradeButton.onClick.AddListener(Upgrade);
        }

        private void OnDisable()
        {
            _presenter.OnMoneyChanged -= UpdatePriceButton;
            _presenter.OnUpgraded -= UpdatePriceButton;
            _presenter.OnUpgraded -= UpdatePlanetInformation;

            _closeButton.onClick.RemoveAllListeners();
            _upgradeButton.onClick.RemoveAllListeners();
        }

        private void Upgrade()
        {
            _presenter.UpgradePlanet();
        }

        private void Show()
        {
            gameObject.SetActive(true);
            UpdatePlanetInformation();
            UpdatePriceButton();
        }

        private void Hide() => gameObject.SetActive(false);

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