using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private RectTransform _rectTransform;

    public ItemInstance ItemInstance { get; private set; }
    public Inventory Inventory { get; private set; }
    public RectTransform RectTransform => _rectTransform;

    public void Construct(ItemInstance itemInstance, Vector2Int _cellSize, Inventory originInventory)
    {
        ItemInstance = itemInstance;
        Inventory = originInventory;
        image.sprite = itemInstance.itemData.Icon;

        Vector2 itemSize = new Vector2(
            _cellSize.x * itemInstance.itemData.Size.x,
            _cellSize.y * itemInstance.itemData.Size.y
        );

        _rectTransform.sizeDelta = itemSize;
    }
}