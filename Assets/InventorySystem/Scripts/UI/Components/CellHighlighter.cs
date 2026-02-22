using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class CellHighlighter : ITickable
    {
        private readonly DragContext _dragContext;
        private readonly InventoryView _inventoryView;
        private readonly InventoryPresenter _presenter;
        private readonly Image _highlightImage;
        private readonly RectTransform _gridContainer;

        private bool _isHighlightActive;
        private bool _isInitialized;

        public CellHighlighter(
            DragContext dragContext,
            InventoryView inventoryView,
            InventoryPresenter presenter,
            Image highlightImage)
        {
            _dragContext = dragContext;
            _inventoryView = inventoryView;
            _presenter = presenter;
            _highlightImage = highlightImage;
            _gridContainer = _inventoryView.GridContainer.GetComponent<RectTransform>();
        }

        public void Tick()
        {
            if (!_dragContext.IsDragging)
            {
                ClearHighlight();
                return;
            }

            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            // Инициализируем при первом использовании
            if (!_isInitialized)
            {
                InitializeHighlight();
            }

            // Проверяем базовые условия
            if (_dragContext.CurrentInventoryCell == null || _dragContext.Item == null)
            {
                ClearHighlight();
                return;
            }

            // Вычисляем позицию начала предмета с учётом смещения клика
            Vector2Int targetPosition = _dragContext.CurrentInventoryCell.MatrixPosition - _dragContext.ClickOffset;
            Vector2Int itemSize = _dragContext.Item.Settings.Size;

            // Проверяем валидность позиции
            if (!IsValidPosition(targetPosition, itemSize))
            {
                ClearHighlight();
                return;
            }

            // Проверяем, что все клеточки свободны
            if (!AreAllCellsFree(targetPosition, itemSize))
            {
                ClearHighlight();
                return;
            }

            // Отображаем подсветку
            ShowHighlight(targetPosition, itemSize);
        }

        private void InitializeHighlight()
        {
            if (_highlightImage != null && _gridContainer != null)
            {
                // Перемещаем картинку в grid контейнер, если она там еще не находится
                if (_highlightImage.transform.parent != _gridContainer)
                {
                    _highlightImage.transform.SetParent(_gridContainer, false);
                }
                _isInitialized = true;
            }
        }

        private bool IsValidPosition(Vector2Int position, Vector2Int size)
        {
            return position.x >= 0 && position.y >= 0 &&
                   position.x + size.x <= _presenter.Width &&
                   position.y + size.y <= _presenter.Height;
        }

        private bool AreAllCellsFree(Vector2Int position, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int cellX = position.x + x;
                    int cellY = position.y + y;

                    if (!_presenter.IsFree(cellX, cellY))
                        return false;
                }
            }

            return true;
        }

        private void ShowHighlight(Vector2Int position, Vector2Int itemSize)
        {
            // Получаем стартовую ячейку (левый верхний угол)
            InventoryCell startCell = _inventoryView.GetCells()[position.x, position.y];

            if (startCell == null)
            {
                ClearHighlight();
                return;
            }

            // Активируем подсветку
            _highlightImage.gameObject.SetActive(true);
            _isHighlightActive = true;

            // Настраиваем размер и позицию
            RectTransform highlightRect = _highlightImage.rectTransform;
            RectTransform startCellRect = startCell.GetComponent<RectTransform>();

            Vector2 cellSize = _inventoryView.CellSize;
            Vector2 highlightSize = new Vector2(
                itemSize.x * cellSize.x,
                itemSize.y * cellSize.y
            );

            highlightRect.sizeDelta = highlightSize;
            highlightRect.anchoredPosition = startCellRect.anchoredPosition;
            highlightRect.SetAsLastSibling();
        }

        private void ClearHighlight()
        {
            if (_isHighlightActive && _highlightImage != null)
            {
                _highlightImage.gameObject.SetActive(false);
                _isHighlightActive = false;
            }
        }
    }
}