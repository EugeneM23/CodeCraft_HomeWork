using System;
using Inventories;
using Inventories.Scripts;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class GameScreen : MonoBehaviour
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
        
        _inventory.SetActive(willBeActive);
        _equipment.SetActive(willBeActive);

        if (willBeActive)
            OnInventoryOpened?.Invoke();
        else
            OnInventoryClosed?.Invoke();
    }

    private void CreateInventory()
    {
        Entity entity = _characterProvider.GetCharacterEntity();

        EquipmentView equipment = _uiFactory.CreateEquipment();
        GameObject inventory = _uiFactory.CreateInventory(equipment.ID);
    }
}