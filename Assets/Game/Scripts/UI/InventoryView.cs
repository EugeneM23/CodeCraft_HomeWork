using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private GridToMatrix gridMatrix;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;

    private List<ItemView> _items = new();

    public void HighlightCell(int x, int y)
    {
        gridMatrix.matrix[x, y].Highlight(highlightedSprite);
    }

    public void AddItem(ItemView itemView, Vector2Int[] positions)
    {
        CellView cellView = gridMatrix.matrix[positions[0].x, positions[0].y];

        ItemView newItem = Instantiate(itemView, cellView.transform);
        _items.Add(newItem);

        RectTransform rt = newItem.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public void Clear()
    {
        for (int i = 0; i < _items.Count; i++) 
            Destroy(_items[i].gameObject);

        _items.Clear();
    }
}