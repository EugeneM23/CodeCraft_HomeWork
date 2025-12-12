using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Inventories;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;

    public ItemData ItemData { get; set; }
    public Sprite Icon => _itemIcon.sprite;
    public IInventoryCollection Presenter { get; set; }
    public Vector2Int MatrixPosition { get; set; }
    public Vector2Int StartPosition { get; set; }

    private readonly List<CellView> _highlightedCells = new();

    public void SetIcon(Sprite icon) => _itemIcon.sprite = icon;

    private void Update()
    {
        ClearHighlight();

        CellView cell = GetCellUnderMouse();
        if (cell == null) return;

        HighlightArea(cell);
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData eventData = new(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            CellView cell = result.gameObject.GetComponent<CellView>();
            if (cell != null) return cell;
        }

        return null;
    }

    private void HighlightArea(CellView centerCell)
    {
        CellView[,] matrix = centerCell.InventoryMatrix;
        Vector2Int center = centerCell.GridPosition;
        Vector2Int size = ItemData.Size;

        int startX = center.x - size.x / 2;
        int startY = center.y;

        MatrixPosition = new Vector2Int(startX, startY);

        bool canPlace = true;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int gx = startX + x;
                int gy = startY + y;

                if (gx < 0 || gy < 0 || gx >= matrix.GetLength(0) || gy >= matrix.GetLength(1))
                {
                    canPlace = false;
                    continue;
                }

                CellView cell = matrix[gx, gy];
                _highlightedCells.Add(cell);

                if (cell.InventoryItem != null)
                    canPlace = false;
            }
        }

        foreach (var cell in _highlightedCells)
            cell.Highlight(canPlace);
    }

    private void ClearHighlight()
    {
        foreach (var cell in _highlightedCells)
            cell.UnHighlight();

        _highlightedCells.Clear();
    }

    private void OnDestroy() => ClearHighlight();
}