using Game.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [field: SerializeField] public ItemID ID { get; private set; }

    public Image icon;
    private Transform originalParent;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;

        // Чтобы предмет отображался поверх всего UI
        transform.SetParent(canvas.transform);

        // Чтобы не блокировал события на клетки
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pos);

        transform.localPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Проверяем, попали ли на клетку
        if (eventData.pointerEnter != null)
        {
            CellView cell = eventData.pointerEnter.GetComponent<CellView>();
            if (cell != null)
            {
                transform.SetParent(cell.transform);
                transform.localPosition = Vector3.zero;
                return;
            }
        }

        // Иначе возвращаемся назад
        transform.SetParent(originalParent);
        transform.localPosition = Vector3.zero;
    }
}