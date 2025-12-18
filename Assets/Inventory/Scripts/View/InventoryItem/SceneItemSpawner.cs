using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class SceneItemSpawner
{
    private readonly Dictionary<string, SceneItem> _itemCatalog = new();

    public void Initialize(List<SceneItem> items)
    {
        foreach (SceneItem item in items)
            _itemCatalog[item.ItemData.Name] = item;
    }

    public void SpawnItem(ItemData itemData, int quantity, Vector3 position)
    {
        if (!_itemCatalog.TryGetValue(itemData.Name, out SceneItem prefab))
        {
            Debug.LogWarning($"Item prefab not found: {itemData.Name}");
            return;
        }

        SceneItem spawnedItem = GameObject.Instantiate(prefab, position, Quaternion.identity);
        spawnedItem.ItemData = itemData;
        spawnedItem.Quantity = quantity;
    }
}