using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private RectTransform _rectTransform;

    public Item Item { get; private set; }
    public InventoryPresenter Presenter { get; private set; }
    public RectTransform RectTransform => _rectTransform;

    public void Construct(Item item, Vector2 _cellSize, InventoryPresenter presenter)
    {
        Item = item;
        Presenter = presenter;
        image.sprite = item.itemData.Icon;

        Vector2 itemSize = new Vector2(
            _cellSize.x * item.itemData.Size.x,
            _cellSize.y * item.itemData.Size.y
        );

        _rectTransform.sizeDelta = itemSize;
    }
}