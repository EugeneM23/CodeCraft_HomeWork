using System.Collections.Generic;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private GridToMatrix gridMatrix;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;
    [SerializeField] private Sprite invalidSprite; // Для недоступных клеток

    private readonly List<ItemView> _items = new();

    public ItemView AddItem(ItemView itemView, Vector2Int[] positions)
    {
        CellView cellView = gridMatrix.matrix[positions[0].x, positions[0].y];

        ItemView newItem = Instantiate(itemView, cellView.transform);
        _items.Add(newItem);

        RectTransform rt = newItem.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return newItem;
    }

    public void Clear()
    {
        for (int i = 0; i < _items.Count; i++)
            Destroy(_items[i].gameObject);

        _items.Clear();
    }
}