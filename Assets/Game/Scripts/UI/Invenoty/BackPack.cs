using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class BackPack : SerializedMonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private SceneItem[] _items;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    [SerializeField] private IItemConsumer _itemConsumer;

    public Inventory Inventory { get; private set; }

    private void Awake()
    {
        Inventory = new Inventory(_columns, _rows);
        
        _itemConsumer.Inventory = Inventory;
        
        _presenter.Inventory = Inventory;

        foreach (SceneItem item in _items)
        {
            Debug.Log(_itemConsumer == null);
            item.ItemData.ItemConsumer = _itemConsumer;
            Inventory.AddItem(item.ItemData, item.Quantity);
        }
    }
}