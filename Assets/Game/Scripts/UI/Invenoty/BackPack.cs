using Inventories;
using UnityEngine;

public class BackPack : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private ItemData[] _items;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;

    public Inventory Inventory { get; private set; }

    private void Awake()
    {
        Inventory = new Inventory(_columns, _rows);
        _presenter.Inventory = Inventory;

        foreach (ItemData item in _items)
        {
            Inventory.AddItem(item);
        }
    }
}