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

    public void SpawnItem(ItemData itemData, Vector3 raycastHitPoint)
    {
        SceneItem item = _itemCatalog[itemData.Name];
        item.ItemData = itemData;
        Instantiate(item, raycastHitPoint, Quaternion.identity);
    }
}