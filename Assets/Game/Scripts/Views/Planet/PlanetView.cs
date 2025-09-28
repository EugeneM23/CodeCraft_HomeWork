using System;
using Modules.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class PlanetView : MonoBehaviour
    {
        [Header("UI Elements")] [SerializeField]
        private GameObject _price;

        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private Image _planetIcon;
        [SerializeField] private SmartButton _button;

        [Header("Income")] [SerializeField] private GameObject _income;
        [SerializeField] private Image _progressImage;
        [SerializeField] private TMP_Text _progressText;

        [Header("Coin")] [SerializeField] private Button _coinButton;
        [SerializeField] private float _coinSpeed = 1f;

        [Inject] private IPlanetPresenter _presenter;

        private void OnEnable() => BindToPresenter();
        private void OnDisable() => UnbindFromPresenter();

        private void BindToPresenter()
        {
            if (_presenter == null) return;

            _presenter.OnUnlocked += UpdateView;
            _presenter.OnStateChanged += UpdateView;
            _presenter.OnIncomeTimeChanged += UpdateProgress;

            _button.OnClick += _presenter.OnClick;
            _button.OnHold += _presenter.OnHold;
            _coinButton.onClick.AddListener(() => _presenter.OnCoinClicked(_coinSpeed));

            UpdateView();
        }

        private void UnbindFromPresenter()
        {
            if (_presenter == null) return;

            _presenter.OnUnlocked -= UpdateView;
            _presenter.OnStateChanged -= UpdateView;
            _presenter.OnIncomeTimeChanged -= UpdateProgress;

            _button.OnClick -= _presenter.OnClick;
            _button.OnHold -= _presenter.OnHold;
            _coinButton.onClick.RemoveAllListeners();
        }

        private void UpdateProgress(float progress, string timeText)
        {
            _progressImage.fillAmount = progress;
            _progressText.text = timeText;
        }

        private void UpdateView()
        {
            if (_presenter == null) return;

            _planetIcon.sprite = _presenter.Icon;

            bool isUnlocked = _presenter.IsUnlocked;
            _lockIcon.SetActive(!isUnlocked);
            _price.SetActive(!isUnlocked);
            _income.SetActive(isUnlocked && !_presenter.IsIncomeReady);
            _coinButton.gameObject.SetActive(_presenter.IsIncomeReady);
        }
    }
}