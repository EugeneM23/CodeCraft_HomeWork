using UnityEngine;
using Inventories;
using Sirenix.OdinInspector;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;

    public Inventory _inventory;

    private void Start()
    {
        _inventory = new Inventory(_columns, _rows);
        _inventory.OnMoved += OnItemMoved;
        
        _view.InitializeGrid(_columns, _rows);

        AddTestItems();
        UpdateView();
    }

    private void OnItemMoved(Item item, Vector2Int newPosition)
    {
        _view.RedrawItem(item, _inventory.GetPositions(item));
    }

    private void UpdateView()
    {
        _view.Clear();
        
        foreach (Item item in _inventory)
            _view.DisplayItem(item, _inventory.GetPositions(item));
    }

    private void AddTestItems()
    {
        Item ring = new Item(ItemID.Ring.ToString(), 1, 1) { ItemID = ItemID.Ring };
        Item ar1 = new Item(ItemID.AR_01.ToString(), 4, 2) { ItemID = ItemID.AR_01 };
        Item ar2 = new Item(ItemID.AR_02.ToString(), 4, 2) { ItemID = ItemID.AR_02 };

        _inventory.AddItem(ring);
        _inventory.AddItem(ar1);
        _inventory.AddItem(ar2);
    }

    [Button] 
    public void Reorganize() 
    { 
        _inventory.ReorganizeSpace(); 
        UpdateView(); 
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            _inventory.OnMoved -= OnItemMoved;
    }
}