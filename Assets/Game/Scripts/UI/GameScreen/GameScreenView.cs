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
        [SerializeField] private RectTransform _mainInventoryRoot;
        
        public RectTransform MainInventoryRoot => _mainInventoryRoot;

        private void OnEnable() => _openInventoryButton.onClick.AddListener(HandleInventoryButtonClick);
        private void OnDisable() => _openInventoryButton.onClick.RemoveListener(HandleInventoryButtonClick);

        private void HandleInventoryButtonClick() => OnInventoryButtonClicked?.Invoke();

        public InventoryBootstrap CreateInventory(InventoryBootstrap inventoryPrefab)
        {
            return Instantiate(inventoryPrefab, _mainInventoryRoot);
        }

        public void Show() => _openInventoryButton.gameObject.SetActive(true);
        public void Hide() => _openInventoryButton.gameObject.SetActive(false);
    }
}