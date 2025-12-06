using Inventories;
using UnityEngine;

public class WeaponMarker : MonoBehaviour
{
    [SerializeField] private InventoryItemCatalog _catalog;
    [SerializeField] public Sprite Icon;

    public Item GetItem()
    {
        var item = new Item(ItemID.AR_01.ToString(), 4, 2, ItemType.Weapon);
        item.ItemID = ItemID.AR_01;
        return item;
    }
}