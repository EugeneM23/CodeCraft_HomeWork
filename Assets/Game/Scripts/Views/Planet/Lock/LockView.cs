using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game
{
    public class LockView : MonoBehaviour
    {
        [SerializeField] private Image _lockIcon;

        [Inject] private readonly ILockViewPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnPlanetUnlocked += Hide;

            if (_presenter.IsPlanetUnlocked)
                Hide();
            else
                Show();
        }

        private void OnDisable() => _presenter.OnPlanetUnlocked -= Hide;

        public void Show() => this.gameObject.SetActive(true);
        public void Hide() => this.gameObject.SetActive(false);
    }
}