using Inventories;
using UnityEngine;

public interface IInventoryCollection
{
    void AddItem(Item item, Vector2Int startPosition = default);
    void RemoveItem(Item item);
    Vector2Int GetItemPosition(Item cellViewItem);
}