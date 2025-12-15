using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class SceneItemSpawner : MonoBehaviour
{
    [SerializeField] private List<SceneItem> _items = new();

    private Dictionary<string, SceneItem> _itemCatalog = new();

    private void Awake()
    {
        foreach (SceneItem item in _items)
            _itemCatalog[item.ItemData.Name] = item;
    }

    public void SpawnItem(ItemData itemData, int quantity, Vector3 position)
    {
        if (!_itemCatalog.TryGetValue(itemData.Name, out SceneItem prefab))
        {
            Debug.LogWarning($"Item prefab not found: {itemData.Name}");
            return;
        }

        SceneItem spawnedItem = Instantiate(prefab, position, Quaternion.identity);
        spawnedItem.ItemData = itemData;
        spawnedItem.Quantity = quantity;
    }
}