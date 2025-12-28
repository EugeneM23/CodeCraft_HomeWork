using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryFactory : MonoBehaviour
{
    private readonly Dictionary<string, SceneItem> _items = new();

    [SerializeField] private DragItem _dragItemPrefab;
    [SerializeField] private InventoryItem _inventoryItemPrefab;
    [SerializeField] private SceneItem[] _itemCatalog;
    [SerializeField] private Canvas _canvas;

    private PrefabPool _prefabPool;

    private void Start()
    {
        _prefabPool = new PrefabPool();
        
        foreach (SceneItem item in _itemCatalog)
            _items[item.ItemData.Name] = item;
    }

    public void SpawnSceneItem(ItemData itemData, int quantity, Vector3 position)
    {
        if (!_items.TryGetValue(itemData.Name, out SceneItem prefab))
        {
            Debug.LogWarning($"Item prefab not found: {itemData.Name}");
            return;
        }

        SceneItem spawnedItem = _prefabPool.Spawn<SceneItem>(prefab.gameObject, null);
        spawnedItem.transform.position = position;
        spawnedItem.transform.rotation = Quaternion.identity;
        spawnedItem.ItemData = itemData;
        spawnedItem.Quantity = quantity;
    }

    public void DeSpawn(GameObject sceneItemGameObject) =>
        _prefabPool.DeSpawn(sceneItemGameObject);

    public DragItem SpawnDragItem(Item item, Vector2 cellSize, Inventory inventory)
    {
        DragItem dragItemitem = _prefabPool.Spawn<DragItem>(_dragItemPrefab.gameObject, _canvas.GetComponent<RectTransform>());
        dragItemitem.transform.SetAsLastSibling();
        dragItemitem.Construct(item, cellSize, inventory);
        return dragItemitem;
    }

    public T SpawnItem<T>(T prefab, RectTransform parent) where T : MonoBehaviour
    {
        T spawnedItem = _prefabPool.Spawn<T>(prefab.gameObject, parent);
        spawnedItem.transform.SetParent(parent);
        return spawnedItem;
    }
}