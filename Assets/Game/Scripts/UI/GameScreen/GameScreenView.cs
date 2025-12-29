using System;
using Game.Scripts.UI.Equipment.Game.Equipment;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenView : MonoBehaviour
    {
        public event Action OnInventoryButtonClicked;

        [SerializeField] private Button _openInventoryButton;
        [SerializeField] private RectTransform _mainInventoryRoot;
        [SerializeField] private RectTransform _secondInventoryRoot;
        [SerializeField] private ItemConsumer _itemConsumer;

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

        public InventoryBootstrap CreateInventory(InventoryBootstrap inventoryPrefab)
        {
            InventoryBootstrap installer = Instantiate(inventoryPrefab, _mainInventoryRoot);
            return installer;
        }


        public void Show() => _openInventoryButton.gameObject.SetActive(true);

        public void Hide() => _openInventoryButton.gameObject.SetActive(false);
    }
}