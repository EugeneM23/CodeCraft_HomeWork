using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private RectTransform _rectTransform;

    public Item Item { get; private set; }
    public Inventory Inventory { get; private set; }
    public RectTransform RectTransform => _rectTransform;

    public void Construct(Item item, Vector2 _cellSize, Inventory originInventory)
    {
        Item = item;
        Inventory = originInventory;
        image.sprite = item.itemData.Icon;

        Vector2 itemSize = new Vector2(
            _cellSize.x * item.itemData.Size.x,
            _cellSize.y * item.itemData.Size.y
        );

        _rectTransform.sizeDelta = itemSize;
    }
}