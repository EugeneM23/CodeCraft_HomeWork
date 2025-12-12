using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour, IInventoryCollection
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    private Inventory _inventory;

    private void Awake()
    {
        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);

        _inventory.OnAdded += OnAddedItem;

        UpdateView();
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < _inventory._cells.GetLength(0); i++)
        {
            for (int j = 0; j < _inventory._cells.GetLength(1); j++)
            {
                _view._cells[i, j].Highlight(_inventory._cells[i, j] != null);
            }
        }

        foreach (ItemInstance item in _inventory)
        {
            Debug.Log(item.uniqueId);
        }
    }

    public bool AddItem(ItemData itemData, Vector2Int startPosition = default)
    {
        ItemInstance instance;

        if (startPosition == default)
        {
            instance = _inventory.AddItem(itemData);
        }
        else
        {
            instance = _inventory.AddItem(itemData, startPosition);
        }

        if (instance != null)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(instance);
            _view.CreateInventoryItem(instance, positions);
            return true;
        }


        return false;
    }

    private void OnAddedItem(ItemInstance instance, Vector2Int position)
    {
    }

    private void UpdateView()
    {
        /*_view.ClearGrid();
        }*/

        // foreach (ItemInstance instance in _inventory)
        // {
        //     Vector2Int[] positions = _inventory.GetItemGridPositions(instance.Item);
        //     ItemBounds bounds = CalculateBounds(positions);
        //     InventoryItem inventoryItem = _view.CreateInventoryItem(instance, bounds.Size, bounds.Position);
        //
        //     inventoryItem.SetItem(instance);
        //
        //     _view.AssignItemToCell(inventoryItem, positions, bounds.Min);
        // }
    }

    private void Update()
    {
        Debug.Log(_inventory.Count);
    }

    public Vector2Int GetItemPosition(ItemInstance instance)
        => instance.GridPosition;

    public void RemoveItem(string id)
    {
        _inventory.RemoveInstance(id);
        _view.RemoveItem(id);
    }


    [Button]
    public void Reorganize()
    {
        // _inventory.ReorganizeSpace();
        // UpdateView();
    }
}