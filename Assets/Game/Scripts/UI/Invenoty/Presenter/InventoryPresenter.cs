using System;
using UnityEngine;
using Inventories;
using Sirenix.OdinInspector;
using System.Collections.Generic;

public partial class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    [SerializeField] public Transform GridParent;
    
    public Inventory _inventory;

    private void Start()
    {
        _inventory = new Inventory(_columns, _rows);

        _view.InitializeGrid(_columns, _rows);

        _inventory.AddItem(new Item(ItemID.AR_01.ToString(), 4, 2) { ItemID = ItemID.AR_01 });
        _inventory.AddItem(new Item(ItemID.AR_02.ToString(), 4, 2) { ItemID = ItemID.AR_02 });

        UpdateView();
    }

    public void RemoveItem(Item item)
    {
        _inventory.RemoveItem(item);
    }

    public void AddItem(Item item, Vector2Int startPosition)
    {
        _inventory.AddItem(item, startPosition);
        //UpdateView();
    }

    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        UpdateView();
    }

    [Button]
    public void AddTestItem()
    {
        Item item = new Item(ItemID.Ring.ToString(), 1, 1) { ItemID = ItemID.Ring };
        _inventory.AddItem(item);
        CreateViewItem(item, _inventory.GetPositions(item));
    }
}