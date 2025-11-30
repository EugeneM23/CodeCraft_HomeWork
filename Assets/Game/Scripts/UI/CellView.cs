using Inventories;
using TMPro;
using UnityEngine;

public class CellView : MonoBehaviour
{
    public TMP_Text Text;
    public Vector2Int GridPosition;
    public Item Item { get; private set; }
    public InventoryItem InventoryItem { get; private set; }

    private void Awake()
    {
        Text.raycastTarget = false;
    }

    public void SetItem(Item item, InventoryItem inventoryItem)
    {
        Item = item;
        InventoryItem = inventoryItem;
        Text.text = item != null ? item.Name : "";
    }

    public void Clear()
    {
        Item = null;
        InventoryItem = null;
        Text.text = "";
    }
}