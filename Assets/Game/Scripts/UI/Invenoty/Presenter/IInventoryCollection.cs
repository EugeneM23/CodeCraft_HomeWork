using Inventories;
using UnityEngine;

public interface IInventoryCollection
{
    bool AddItem(ItemData itemData, Vector2Int startPosition = default);
   
}