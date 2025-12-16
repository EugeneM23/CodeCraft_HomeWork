using UnityEngine;
using UnityEngine.EventSystems;

public class DoubleClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private float doubleClickTime = 0.3f;

    private ItemUseCase _itemUseCase;

    private float lastClickTime = 0f;
    private IItemConsumer _itemConsumer;

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log(_itemConsumer == null);
        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick <= doubleClickTime)
            _itemUseCase.Invoke(_itemConsumer);

        lastClickTime = Time.time;
    }

    public void SetUpUseCase(ItemUseCase itemUseCase, IItemConsumer itemConsumer)
    {
        _itemUseCase = itemUseCase;
        _itemConsumer = itemConsumer;
    }
}