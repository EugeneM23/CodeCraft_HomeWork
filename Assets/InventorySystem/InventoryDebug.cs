using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class InventoryDebug : MonoBehaviour
{
    [Inject] private DiContainer _container;

    [SerializeField] private InventoryView _inventoryPrefab;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _openInventoryButton;

    private GameObject _inventory;
    private InventoryView _inventoryView;

    // События для камеры
    public event Action OnInventoryOpened;
    public event Action OnInventoryClosed;

    private void Start()
    {
        _openInventoryButton.onClick.AddListener(ToggleInventory);
    }

    private void OnDestroy()
    {
        _openInventoryButton.onClick.RemoveListener(ToggleInventory);
        UnsubscribeFromInventory();
    }

    private void ToggleInventory()
    {
        // Если инвентарь еще не создан - создаем
        if (_inventory == null)
        {
            CreateInventory();
            return;
        }

        // Переключаем видимость
        _inventory.SetActive(!_inventory.activeSelf);
    }

    private void CreateInventory()
    {
        _inventory = _container.InstantiatePrefab(_inventoryPrefab, _canvas.transform);
        _inventoryView = _inventory.GetComponent<InventoryView>();

        // Подписываемся на события инвентаря
        _inventoryView.OnInventoryOpened += HandleInventoryOpened;
        _inventoryView.OnInventoryClosed += HandleInventoryClosed;

        OnInventoryOpened?.Invoke();
    }

    private void UnsubscribeFromInventory()
    {
        if (_inventoryView != null)
        {
            _inventoryView.OnInventoryOpened -= HandleInventoryOpened;
            _inventoryView.OnInventoryClosed -= HandleInventoryClosed;
        }
    }

    private void HandleInventoryOpened()
    {
        OnInventoryOpened?.Invoke();
    }

    private void HandleInventoryClosed()
    {
        OnInventoryClosed?.Invoke();
    }

    [Button]
    public void RemoveItem(Vector2Int position)
    {
        if (_inventory == null) return;

        var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
        bool removeItem = inventory.RemoveItem(position);
        Debug.Log("Item removed: " + removeItem);
    }

    [Button]
    public void AddItem(SceneItem item)
    {
        if (_inventory == null) return;

        var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
        inventory.AddItem(item.itemSettings);
    }
}