using System;
using System.Collections.Generic;
using Inventories;
using Inventories.Scripts;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class GameScreen : MonoBehaviour
{
    [SerializeField] private Button _openInventoryButton;
    [SerializeField] private List<SceneItem> _sceneItems = new();

    private UIFactory _uiFactory;
    private PlayerCharacterProvider _characterProvider;

    [Inject]
    public void Construct(PlayerCharacterProvider characterProvider, UIFactory uiFactory)
    {
        _characterProvider = characterProvider;
        _uiFactory = uiFactory;
    }

    public event Action OnInventoryOpened;
    public event Action OnInventoryClosed;

    private void OnEnable()
    {
        _openInventoryButton.onClick.AddListener(ToggleInventory);
    }

    private void OnDisable()
    {
        _openInventoryButton.onClick.RemoveListener(ToggleInventory);
    }

    private void ToggleInventory()
    {
        var entity = _characterProvider.GetCharacterEntity();

        if (!entity.TryResolveComponent<InventoryView>(out var inventoryView))
        {
            CreateInventoryAndEquipment(entity);
            return;
        }

        ToggleInventory(entity, inventoryView);
    }

    private void CreateInventoryAndEquipment(Entity entity)
    {
        var equipmentView = _uiFactory.CreateEquipment();
        var inventoryView = _uiFactory.CreateInventory(equipmentView.ID, new Vector2Int(7, 5), _sceneItems);
        
        OnInventoryOpened?.Invoke();
    }

    private void ToggleInventory(Entity entity, InventoryView inventoryView)
    {
        entity.TryResolveComponent<EquipmentView>(out var equipmentView);
        bool willBeActive = !inventoryView.gameObject.activeSelf;

        inventoryView.gameObject.SetActive(willBeActive);
        equipmentView.gameObject.SetActive(willBeActive);

        if (willBeActive)
            OnInventoryOpened?.Invoke();
        else
            OnInventoryClosed?.Invoke();
    }
}