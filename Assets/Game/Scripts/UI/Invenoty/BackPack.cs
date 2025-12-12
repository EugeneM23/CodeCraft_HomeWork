using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class BackPack : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private ItemData[] _items;

    private void Start()
    {
        foreach (var item in _items) 
            _presenter.AddItem(item);
    }

    [Button]
    public void AddItem()
    {
    }

    [Button]
    public void RemoveItem()
    {
    }
}