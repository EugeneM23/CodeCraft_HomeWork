using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public interface IInventoryPresenter
{
    event Action<Item, Vector2Int[]> OnItemAdded;
    event Action<Item, Vector2Int[]> OnItemRemoved;
    event Action OnReorganize;

    int Height { get; }
    int Width { get; }

    IEnumerable<KeyValuePair<Item, Vector2Int[]>> GetItems();
    void RemoveItem(Item item);
    bool AddItem(Item item, Vector2Int position);
    Vector2Int GetItemPosition(string itemID);
    bool IsFree(int x, int y);
    bool IsFree(Vector2Int position);
    void Reorganize();
}