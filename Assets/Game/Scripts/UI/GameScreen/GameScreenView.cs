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
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [SerializeField] private RectTransform _canvas;

        private InventoryInstaller _inventory;

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

        public InventoryInstaller CreateInventory()
        {
            _inventory = Instantiate(_inventoryPrefab, _canvas);
            return _inventory;
        }
    }
}