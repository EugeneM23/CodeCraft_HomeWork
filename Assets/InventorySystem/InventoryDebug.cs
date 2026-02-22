using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class InventoryDebug : MonoBehaviour
{
    [Inject] private DiContainer _container;

    [SerializeField] private InventoryView _inventoryPrefab;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _openInventoryButton;

    private GameObject _inventory;

    private void Start()
    {
        _openInventoryButton.onClick.AddListener(ToggleInventory);
    }

    private void OnDestroy()
    {
        _openInventoryButton.onClick.RemoveListener(ToggleInventory);
    }

    private void ToggleInventory()
    {
        // Если инвентарь еще не создан - создаем
        if (_inventory == null)
        {
            CreateInventory();
            return;
        }

        // Если создан - переключаем видимость
        _inventory.SetActive(!_inventory.activeSelf);
    }

    private void CreateInventory()
    {
        _inventory = _container.InstantiatePrefab(_inventoryPrefab, _canvas.transform);
    }

    [Button]
    public void RemoveItem(Vector2Int position)
    {
        if (_inventory == null) return;

        var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
        bool removeItem = inventory.RemoveItem(position);
        Debug.Log("Item removed: " + removeItem);
    }

    [Button]
    public void AddItem(SceneItem item)
    {
        if (_inventory == null) return;

        var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
        inventory.AddItem(item.itemSettings);
    }
}