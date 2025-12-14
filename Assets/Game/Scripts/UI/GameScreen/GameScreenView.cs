using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenView : MonoBehaviour
    {
        public event Action<bool> OnInventoryButtonClicked;

        [SerializeField] private Button _openInventoryButton;
        [SerializeField] private InventoryView _inventoryPrefab;

        private InventoryView _inventoryContainer;

        private void OnEnable()
        {
            _openInventoryButton.onClick.AddListener(OnInventoryEnable);
        }

        private void OnDisable()
        {
            _openInventoryButton.onClick.RemoveListener(OnInventoryEnable);
        }

        private void OnInventoryEnable()
        {
            bool needOpen = _inventoryContainer == null || !_inventoryContainer.gameObject.activeSelf;
            OnInventoryButtonClicked?.Invoke(needOpen);
        }

        public void OpenInventory()
        {
            if (_inventoryContainer == null)
                _inventoryContainer = Instantiate(_inventoryPrefab, transform);
            else
                _inventoryContainer.gameObject.SetActive(true);
        }

        public void CloseInventory()
        {
            if (_inventoryContainer != null) 
                _inventoryContainer.gameObject.SetActive(false);
        }
    }
}