using Inventories;
using UnityEngine;

public class Entity : MonoBehaviour
{
    [SerializeField] private ItemConsumer _itemConsumer;
    [SerializeField] private InventoryBootstrap _inventoryPrefab;

    public ItemConsumer ItemConsumer => _itemConsumer;
    public InventoryBootstrap InventoryPrefab => _inventoryPrefab;
    public InventoryPresenter InventoryPresenter { get; private set; }

    public void SetInventoryPresenter(InventoryPresenter presenter)
    {
        InventoryPresenter = presenter;
        InventoryPresenter.Owner = _itemConsumer;
        _itemConsumer.SetInventory(InventoryPresenter);
    }
}