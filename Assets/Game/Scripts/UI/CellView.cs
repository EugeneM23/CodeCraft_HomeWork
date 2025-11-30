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
}