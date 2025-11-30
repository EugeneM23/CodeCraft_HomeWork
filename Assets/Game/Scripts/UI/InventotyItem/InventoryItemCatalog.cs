using System.Collections.Generic;
using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class InventoryItemCatalog : SerializedMonoBehaviour
{
    [OdinSerialize] private Dictionary<ItemID, InventoryItemData> _items = new();

    public bool GetItemData(ItemID itemName, out InventoryItemData data)
    {
        return _items.TryGetValue(itemName, out data);
    }
}