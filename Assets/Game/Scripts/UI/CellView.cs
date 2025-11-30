using System;
using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CellView : MonoBehaviour
{
    public TMP_Text Text;
    public Vector2Int GridPosition; // Единственная позиция - позиция в сетке
    public Item Item { get; private set; }

    private void Awake()
    {
        Text.raycastTarget = false;
    }

    public void SetItem(Item item)
    {
        Item = item;
        Text.text = item != null ? item.Name : "";
    }

    public void Clear()
    {
        Item = null;
        Text.text = "";
    }
}