using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenView : MonoBehaviour
    {
        public event Action OnInventoryButtonClicked;

        [SerializeField] private Button _openInventoryButton;
        [SerializeField] private InventoryView _inventoryPrefab;

        private InventoryView _inventoryContainer;

        private void Start()
        {
            _inventoryContainer = Instantiate(_inventoryPrefab, transform);
            _inventoryContainer.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _openInventoryButton.onClick.AddListener(HandleInventoryButtonClick);
        }

        private void OnDisable()
        {
            _openInventoryButton.onClick.RemoveListener(HandleInventoryButtonClick);
        }

        private void HandleInventoryButtonClick()
        {
            OnInventoryButtonClicked?.Invoke();
        }

        public void EnableInventory()
        {
            _inventoryContainer.gameObject.SetActive(!_inventoryContainer.gameObject.activeSelf);
        }
    }
}