using Game.Scripts.UI;
using Inventories;
using UnityEngine;
using System.Collections.Generic;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private ItemCatalog _itemCatalog;
    [SerializeField] private InventoryView _view;
    [SerializeField] private Bag _bag;

    private Inventory _bagInventory;
    private Dictionary<ItemView, Item> _viewToModelMap = new();

    private void Start()
    {
        _bagInventory = _bag.inventory;
        _bag.OnStateChanged += OnStateChanged;
        
        RenderInventory();
    }

    private void RenderInventory()
    {
        _view.Clear();
        _viewToModelMap.Clear();


        foreach (Item item in _bagInventory)
        {
            ItemView itemView = _itemCatalog.GetItem(item);
            Vector2Int[] positions = _bagInventory.GetPositions(item);
            
            if (positions != null && positions.Length > 0)
            {
                ItemView instantiatedView = _view.AddItem(itemView, positions);
                if (instantiatedView != null)
                {
                    _viewToModelMap[instantiatedView] = item;
                }
            }
        }
    }

    private void OnStateChanged()
    {
        RenderInventory();
    }


    public bool TryMoveItem(ItemView itemView, Vector2Int newPosition)
    {
        if (!_viewToModelMap.TryGetValue(itemView, out Item item))
            return false;

        bool success = _bagInventory.MoveItem(item, newPosition);
        
        if (success)
        {
            RenderInventory();
        }

        return success;
    }
}