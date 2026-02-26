using System;
using Inventories;
using Inventories.Scripts;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class InventoryDebug : MonoBehaviour
{
    [Inject] private DiContainer _container;
    [Inject] private PlayerCharacterProvider _characterProvider;

    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _openInventoryButton;

    private GameObject _inventory;
    private GameObject _equipment;

    public event Action OnInventoryOpened;
    public event Action OnInventoryClosed;

    private void Start()
    {
        _openInventoryButton.onClick.AddListener(ToggleInventory);
    }

    private void OnDestroy()
    {
        _openInventoryButton.onClick.RemoveListener(ToggleInventory);
    }

    private void ToggleInventory()
    {
        if (_inventory == null)
        {
            CreateInventory();
            return;
        }

        bool willBeActive = !_inventory.activeSelf;
        SetInventoryActive(willBeActive);
    }

    private void CreateInventory()
    {
        Entity entity = _characterProvider.GetCharacterEntity();

        var inventoryPrefab = entity.ResolveComponent<InventoryView>(UIPrefabs.InventoryPrefab);
        _inventory = _container.InstantiatePrefabForComponent<InventoryView>(inventoryPrefab, _canvas.transform).gameObject;

        var equipmentPrefab = entity.ResolveComponent<EquipmentView>(UIPrefabs.EquipmentPrefab);
        _equipment = _container.InstantiatePrefabForComponent<EquipmentView>(equipmentPrefab, _canvas.transform).gameObject;

        SubscribeToInventoryClose();
        SetInventoryActive(true);
    }

    private void SubscribeToInventoryClose()
    {
        var inventoryEntity = _inventory.GetComponent<Entity>();
        var presenter = inventoryEntity.ResolveComponent<InventoryPresenter>();
        presenter.OnClose += HandleInventoryClose;
    }

    private void SetInventoryActive(bool isActive)
    {
        _inventory.SetActive(isActive);
        _equipment.SetActive(isActive);

        if (isActive)
            OnInventoryOpened?.Invoke();
        else
            OnInventoryClosed?.Invoke();
    }

    private void HandleInventoryClose()
    {
        SetInventoryActive(false);
    }

    [Button]
    public void RemoveItem(Vector2Int position)
    {
        if (_inventory == null) return;

        var inventory = GetInventory();
        bool removed = inventory.RemoveItem(position);
        Debug.Log("Item removed: " + removed);
    }

    [Button]
    public void AddItem(SceneItem item)
    {
        if (_inventory == null) return;

        var inventory = GetInventory();
        inventory.AddItem(item.itemSettings);
    }

    private Inventory GetInventory()
    {
        return _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
    }
}