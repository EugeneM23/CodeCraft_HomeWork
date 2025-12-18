using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryFactory
{
    private readonly Dictionary<string, SceneItem> _itemCatalog = new();
    private readonly PrefabPool _prefabPool;
    private readonly DragItem _dragItemPrefab;
    private readonly Vector2 _cellSize;
    private readonly Inventory _inventory;
    private readonly Transform _parent;

    public InventoryFactory(PrefabPool prefabPool, DragItem dragItemPrefab, Vector2 cellSize, Inventory inventory,
        Transform parent)
    {
        _prefabPool = prefabPool;
        _dragItemPrefab = dragItemPrefab;
        _cellSize = cellSize;
        _inventory = inventory;
        _parent = parent;
    }

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

        SceneItem spawnedItem = _prefabPool.Spawn<SceneItem>(prefab.gameObject);
        spawnedItem.transform.position = position;
        spawnedItem.transform.rotation = Quaternion.identity;
        spawnedItem.ItemData = itemData;
        spawnedItem.Quantity = quantity;
    }

    public void DeSpawn(GameObject sceneItemGameObject) =>
        _prefabPool.DeSpawn(sceneItemGameObject);

    public DragItem CreateDragItem(ItemInstance itemInstance)
    {
        DragItem item = _prefabPool.Spawn<DragItem>(_dragItemPrefab.gameObject);
        item.Construct(itemInstance, _cellSize, _inventory);
        item.transform.SetParent(_parent);
        return item;
    }

    public T SpawnItem<T>(T prefab, RectTransform parent) where T : MonoBehaviour
    {
        T spawnedItem = _prefabPool.Spawn<T>(prefab.gameObject, parent);
        spawnedItem.transform.SetParent(parent);
        return spawnedItem;
    }
}