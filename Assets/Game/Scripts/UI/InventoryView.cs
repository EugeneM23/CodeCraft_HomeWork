using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private GridToMatrix gridMatrix;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;

    public void HighlightCell(int x, int y)
    {
        gridMatrix.matrix[x, y].Highlight(highlightedSprite);
    }

    public void AddItem(ItemView itemView, Vector2Int[] positions)
    {
        CellView cellView = gridMatrix.matrix[positions[0].x, positions[0].y];

        ItemView newItem = Instantiate(itemView, cellView.transform);

        RectTransform rt = newItem.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}