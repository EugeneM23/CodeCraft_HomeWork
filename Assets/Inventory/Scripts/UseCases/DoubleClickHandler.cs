using UnityEngine;
using UnityEngine.EventSystems;
using Inventories;

public class DoubleClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private float doubleClickTime = 0.3f;

    private ItemUseCase _itemUseCase;
    private Inventory _inventory;

    private float lastClickTime = 0f;
    private ItemInstance _itemInstance;

    public void OnPointerClick(PointerEventData eventData)
    {
        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick <= doubleClickTime)
        {
            _itemInstance.ItemUseCase.Invoke(_inventory, _itemInstance);
        }

        lastClickTime = Time.time;
    }

    public void SetUpUseCase(ItemInstance itemInstance, Inventory inventory)
    {
        _itemInstance = itemInstance;
        _inventory = inventory;
    }
}