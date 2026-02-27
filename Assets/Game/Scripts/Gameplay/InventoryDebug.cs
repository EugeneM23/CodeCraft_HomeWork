using System;
using Inventories;
using Inventories.Scripts;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class InventoryDebug : MonoBehaviour
{
    [Inject] private UIFactory _uiFactory;
    [Inject] private PlayerCharacterProvider _characterProvider;

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
        (_inventory, _equipment) = _uiFactory.CreateInventoryWithEquipment(entity);

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
}