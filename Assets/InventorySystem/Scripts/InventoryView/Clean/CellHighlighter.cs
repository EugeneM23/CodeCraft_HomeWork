using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class CellHighlighter : ITickable
    {
        private readonly DragContext _dragContext;
        private readonly InventoryView _inventoryView;

        private Image _highlightImage;
        private RectTransform _gridContainer;
        private bool _isHighlightActive = false;

        public CellHighlighter(DragContext dragContext, InventoryView inventoryView)
        {
            _dragContext = dragContext;
            _inventoryView = inventoryView;
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
            if (_dragContext.CurrentCell == null || _dragContext.Item == null)
            {
                ClearHighlight();
                return;
            }

            // Вычисляем позицию начала предмета с учётом смещения клика
            Vector2Int targetPosition = _dragContext.CurrentCell.MatrixPosition - _dragContext.ClickOffset;
            Vector2Int itemSize = _dragContext.Item.itemData.Size;

            // Проверяем, находится ли targetPosition в пределах сетки
            if (targetPosition.x < 0 || targetPosition.y < 0 ||
                targetPosition.x + itemSize.x > _inventoryView.Presenter.Width ||
                targetPosition.y + itemSize.y > _inventoryView.Presenter.Height)
            {
                ClearHighlight();
                return;
            }

            // Проверяем, что все клеточки свободны
            bool allCellsFree = true;
            for (int y = 0; y < itemSize.y; y++)
            {
                for (int x = 0; x < itemSize.x; x++)
                {
                    int cellX = targetPosition.x + x;
                    int cellY = targetPosition.y + y;

                    if (!_dragContext.CurrentCell.Presenter.IsFree(cellX, cellY))
                    {
                        allCellsFree = false;
                        break;
                    }
                }

                if (!allCellsFree) break;
            }

            if (!allCellsFree)
            {
                ClearHighlight();
                return;
            }

            // Получаем первую клеточку (левый верхний угол)
            Cell startCell = _inventoryView.GetCells()[targetPosition.x, targetPosition.y];

            if (startCell == null)
            {
                ClearHighlight();
                return;
            }

            // Инициализируем highlight image при первом использовании
            if (_highlightImage == null)
            {
                _highlightImage = _inventoryView.SelectedArea;
                _gridContainer = _inventoryView.GridContainer.GetComponent<RectTransform>();

                if (_highlightImage != null && _gridContainer != null)
                {
                    // Перемещаем картинку в grid контейнер, если она там еще не находится
                    if (_highlightImage.transform.parent != _gridContainer)
                    {
                        _highlightImage.transform.SetParent(_gridContainer, false);
                    }
                }
            }

            if (_highlightImage != null)
            {
                // Активируем картинку
                _highlightImage.gameObject.SetActive(true);
                _isHighlightActive = true;

                // Получаем RectTransform картинки и первой клеточки
                RectTransform highlightRect = _highlightImage.rectTransform;
                RectTransform startCellRect = startCell.GetComponent<RectTransform>();

                // Вычисляем размер области подсветки
                Vector2 cellSize = _inventoryView.CellSize;
                Vector2 highlightSize = new Vector2(
                    itemSize.x * cellSize.x,
                    itemSize.y * cellSize.y
                );

                // Устанавливаем размер и позицию
                highlightRect.sizeDelta = highlightSize;
                highlightRect.anchoredPosition = startCellRect.anchoredPosition;

                // Поднимаем на передний план
                highlightRect.SetAsLastSibling();
            }
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