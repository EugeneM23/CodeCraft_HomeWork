using System;
using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CellView : MonoBehaviour, IPointerDownHandler
{
    public event Action<Item, Vector2Int> OnCellClickedDown;
    public event Action<Vector2Int, Vector2Int> OnCellClickedUp;

    public TMP_Text Text;
    public Item Item;
    public Vector2Int position;
    public Vector2Int itemPosition;
    public InventoryView inventoryView;

    private void Awake()
    {
        Text.raycastTarget = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventoryView._selectedItem != null)
        {
            OnCellClickedUp?.Invoke(position, Vector2Int.zero);
            return;
        }

        OnCellClickedDown?.Invoke(Item, itemPosition);
    }
}