using Inventories;
using UnityEngine;

public class BackPack : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;

    private void Start()
    {
        Item item = new Item(ItemID.AR_01.ToString(), 4, 2);
        item.ItemID = ItemID.AR_01;
        Item item1 = new Item(ItemID.AR_02.ToString(), 4, 2);
        item1.ItemID = ItemID.AR_02;
        
        _presenter.AddItem(item);
        _presenter.AddItem(item1);
    }
}