using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptyCell;
    [SerializeField] private Sprite _occupiedCell;
    [SerializeField] private Sprite _arroredCell; 

    public Vector2Int ItemMatrixPosition;
    public Vector2Int GridPosition;
    public Item Item { get; private set; }
    public InventoryItem InventoryItem { get; private set; }

    public void SetItem(Item item, InventoryItem inventoryItem)
    {
        Item = item;
        InventoryItem = inventoryItem;
    }

    public void Clear()
    {
        Item = null;
        InventoryItem = null;
    }

    public void Highlight()
    {
        _backGround.sprite = _occupiedCell;
    }

    public void UnHighlight()
    {
        _backGround.sprite = _emptyCell;
    }
    public void SetErrorHighlight()
    {
        _backGround.sprite = _arroredCell;
    }
}