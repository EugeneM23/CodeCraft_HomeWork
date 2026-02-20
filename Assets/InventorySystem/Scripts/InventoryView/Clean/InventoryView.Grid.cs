using UnityEngine;

namespace Inventories
{
    public partial class InventoryView
    {
        private void CreateGrid()
        {
            for (int y = 0; y < _adapter.Height; y++)
            {
                for (int x = 0; x < _adapter.Width; x++)
                {
                    // Создаем ячейку и получаем её компоненты
                    var cell = _container.InstantiatePrefab(_cellPrefab, _gridContainer);
                    var cellComponent = cell.GetComponent<Cell>();

                    // Настраиваем размер и позицию ячейки
                    var rect = cell.GetComponent<RectTransform>();
                    rect.sizeDelta = _cellSize;
                    rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

                    // Сохраняем позицию в матрице и добавляем в массив
                    cellComponent.MatrixPosition = new Vector2Int(x, y);
                    _cells[x, y] = cellComponent;
                }
            }
        }
    }
}