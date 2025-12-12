using Inventories;
using UnityEngine;

public interface IInventoryCollection
{
    bool AddItem(ItemData itemData, Vector2Int startPosition = default);
    void RemoveItem(string id);
    Vector2Int GetItemPosition(ItemInstance item);
}