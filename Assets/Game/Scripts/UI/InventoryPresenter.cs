using Game.Scripts.UI;
using Inventories;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private ItemCatalog _itemCatalog;
    [SerializeField] private InventoryView _view;
    [SerializeField] private Bag _bag;

    private Inventory _bagInventory;

    private void Start()
    {
        _bagInventory = _bag.inventory;
        _bag.OnStateChanged += OnStateChanged;

        foreach (Item item in _bagInventory)
        {
            ItemView itemView = _itemCatalog.GetItem(item);
            Vector2Int[] positions = _bagInventory.GetPositions(item);
            _view.AddItem(itemView, positions);
        }
    }

    private void OnStateChanged()
    {
        _view.Clear();
        foreach (Item item in _bagInventory)
        {
            ItemView itemView = _itemCatalog.GetItem(item);
            Vector2Int[] positions = _bagInventory.GetPositions(item);
            _view.AddItem(itemView, positions);
        }
    }

    public void HighlightCell(int x, int y)
    {
    }
}