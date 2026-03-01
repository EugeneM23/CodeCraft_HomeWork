using System;
using System.Collections.Generic;
using System.Linq;
using Inventories;
using Inventories.Scripts;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class GameScreen : MonoBehaviour
{
    [SerializeField] private Button _openInventoryButton;
    [SerializeField] private List<SceneItem> _sceneItems = new();
    [SerializeField] private List<SceneItem> _equipmentItems = new();

    private UIFactory _uiFactory;
    private PlayerCharacterProvider _characterProvider;

    [Inject]
    public void Construct(PlayerCharacterProvider characterProvider, UIFactory uiFactory)
    {
        _characterProvider = characterProvider;
        _uiFactory = uiFactory;
    }

    private void Start()
    {
        CreateInventoryAndEquipment();
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
        InventoryView inventoryView = entity.ResolveComponent<InventoryView>();

        ToggleInventory(entity, inventoryView);
    }

    private void CreateInventoryAndEquipment()
    {
        var initialEquipment = _equipmentItems.Count > 0
            ? _equipmentItems.Select(x => new Item(x.itemSettings, new Vector2Int())).ToList()
            : null;

        var equipmentView = _uiFactory.CreateEquipment(initialEquipment);
        var inventoryView = _uiFactory.CreateInventory(equipmentView.ID, new Vector2Int(7, 5), _sceneItems);

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