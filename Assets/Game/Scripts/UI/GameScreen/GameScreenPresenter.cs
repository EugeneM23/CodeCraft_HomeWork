using UnityEngine;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameScreenView _view;

        private void OnEnable()
        {
            _view.OnInventoryButtonClicked += OpenInventory;
        }

        private void OnDisable()
        {
            _view.OnInventoryButtonClicked -= OpenInventory;
        }

        private void OpenInventory(bool enable)
        {
            if (enable)
                _view.OpenInventory();
            else
                _view.CloseInventory();
        }
    }
}