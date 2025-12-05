using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Inventories;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;

    public Item Item { get; set; }
    public Sprite Icon => _itemIcon.sprite;
    public IInventoryCollection Presenter { get; set; }
    public Vector2Int MatrixPosition { get; set; }
    public Vector2Int StartPosition { get; set; }

    private readonly List<CellView> _highlightedCells = new();

    public void SetIcon(Sprite icon)
    {
        _itemIcon.sprite = icon;
    }

    private void Update()
    {
        CheckUICellUnderMouse();
    }

    private void CheckUICellUnderMouse()
    {
        ClearHighlightedCells();

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        CellView cell = null;

        foreach (var r in results)
        {
            var cv = r.gameObject.GetComponent<CellView>();
            if (cv != null)
            {
                cv.Highlight(true); // временная подсветка центральной клетки
                cell = cv;

                Vector2Int size = Item.Size;
                Vector2Int center = cv.GridPosition;

                // Верхняя левая клетка области
                int startX = center.x - size.x / 2;
                int startY = center.y; // верхняя строка = центральная клетка

                MatrixPosition = new Vector2Int(startX, startY);

                // Debug для проверки
                Debug.Log($"[DragItem] Item.Size = {size}");
                Debug.Log($"[DragItem] Center Cell = {center}");
                Debug.Log($"[DragItem] Computed Start (top-left) = {MatrixPosition}");

                break; // берём только первую CellView под мышью
            }
        }

        if (cell == null)
            return;

        HighlightArea(cell);
    }

    private void HighlightArea(CellView centerCell)
    {
        Vector2Int size = Item.Size;
        CellView[,] matrix = centerCell.InventoryMatrix;
        Vector2Int center = centerCell.GridPosition;

        int startX = center.x - size.x / 2;
        int startY = center.y; // верхняя строка = центральная клетка

        bool allFree = true;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int gx = startX + x;
                int gy = startY + y;

                // Проверка выхода за границы
                if (gx < 0 || gy < 0 ||
                    gx >= matrix.GetLength(0) ||
                    gy >= matrix.GetLength(1))
                {
                    allFree = false;
                    continue;
                }

                CellView c = matrix[gx, gy];
                _highlightedCells.Add(c);

                if (c.InventoryItem != null)
                    allFree = false;

                Debug.Log($"[DragItem] Highlight Cell = ({gx},{gy})");
            }
        }

        // Подсветка всех ячеек области
        foreach (var c in _highlightedCells)
            c.Highlight(allFree);
    }

    private void ClearHighlightedCells()
    {
        foreach (var c in _highlightedCells)
            c.UnHighlight();

        _highlightedCells.Clear();
    }

    private void OnDestroy()
    {
        ClearHighlightedCells();
    }
}
