using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class IncomeView : MonoBehaviour
    {
        [SerializeField] private Image _progressImage;
        [SerializeField] private TMP_Text _progressText;

        [Inject] private IIncomeViewPresenter _presenter;

        private void Start()
        {
            _presenter.OnIncomeTimeChanged += UpdateProgress;
            _presenter.OnStateChanged += SetViewActive;

            SetViewActive(_presenter.IsPlanetUnlocked);
        }

        private void OnDestroy()
        {
            _presenter.OnIncomeTimeChanged -= UpdateProgress;
            _presenter.OnStateChanged -= SetViewActive;
        }

        private void SetViewActive(bool isActive) => gameObject.SetActive(isActive);

        private void UpdateProgress(float progress, string timeText)
        {
            _progressImage.fillAmount = progress;
            _progressText.text = timeText;
        }
    }
}