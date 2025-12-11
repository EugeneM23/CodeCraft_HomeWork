using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class BackPack : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    private Item _item;

    private void Start()
    {
        Item item = new Item(ItemID.AR_01.ToString(), 4, 2);
        item.ItemID = ItemID.AR_01;

        Item item1 = new Item(ItemID.AR_02.ToString(), 4, 2);
        item1.ItemID = ItemID.AR_02;
        Item item2 = new Item(ItemID.Ammo.ToString(), 1, 1);
        item2.ItemID = ItemID.Ammo;

        _presenter.AddItem(item2);
        _presenter.AddItem(item);
        _presenter.AddItem(item1);
    }

    [Button]
    public void AddItem()
    {
        _item = new Item(ItemID.AR_02.ToString(), 3, 2)
        {
            ItemID = ItemID.AR_02
        };
        _presenter.AddItem(_item);
    }

    [Button]
    public void RemoveItem()
    {
        _presenter.RemoveItem(_item);
    }
}