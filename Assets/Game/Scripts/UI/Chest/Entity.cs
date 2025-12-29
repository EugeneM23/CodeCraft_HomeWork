using Inventories;
using UnityEngine;

public class Entity : MonoBehaviour
{
    [SerializeField] private ItemConsumer _itemConsumer;
    [field: SerializeField] public InventoryBootstrap InventoryPrefab { private set; get; }
    public InventoryPresenter InventoryPresenter { get; private set; }

    public void SetPresenter(InventoryPresenter mainInventoryPresenter)
    {
        InventoryPresenter = mainInventoryPresenter;
    }
}