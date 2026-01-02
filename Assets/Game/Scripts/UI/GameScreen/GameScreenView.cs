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

        private void OnEnable() => _openInventoryButton.onClick.AddListener(HandleInventoryButtonClick);
        private void OnDisable() => _openInventoryButton.onClick.RemoveListener(HandleInventoryButtonClick);

        private void HandleInventoryButtonClick() => OnInventoryButtonClicked?.Invoke();

        public InventoryBootstrap CreateInventory(InventoryBootstrap prefab)
        {
            return Instantiate(prefab, _mainInventoryRoot);
        }

        public void Show() => _openInventoryButton.gameObject.SetActive(true);
        public void Hide() => _openInventoryButton.gameObject.SetActive(false);
    }
}