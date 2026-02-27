using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    /// <summary>
    /// Код написан чатом, я не знаю что тут происходит 🤓 
    /// </summary>
    public partial class InventoryView : IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _highlightImage;

        [Inject] private DragContext _dragContext;

        private bool _isHighlightActive;
        private bool _isPointerOver;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            ClearHighlight();
        }

        private void Update()
        {
            if (!_isPointerOver || !_dragContext.IsDragging)
            {
                ClearHighlight();
                return;
            }

            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            if (_dragContext.CurrentCellView == null || _dragContext.Item == null)
            {
                ClearHighlight();
                return;
            }

            Vector2Int targetPosition = _dragContext.CurrentCellView.MatrixPosition - _dragContext.ClickOffset;
            Vector2Int itemSize = _dragContext.Item.Settings.Size;

            if (!IsValidPosition(targetPosition, itemSize))
            {
                ClearHighlight();
                return;
            }

            if (!AreAllCellsFree(targetPosition, itemSize))
            {
                ClearHighlight();
                return;
            }

            ShowHighlight(targetPosition, itemSize);
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
            CellView startCellView = _cells[position.x, position.y];

            if (startCellView == null)
            {
                ClearHighlight();
                return;
            }

            if (_highlightImage.transform.parent != _gridContainer)
                _highlightImage.transform.SetParent(_gridContainer, false);

            _highlightImage.gameObject.SetActive(true);
            _isHighlightActive = true;

            RectTransform highlightRect = _highlightImage.rectTransform;
            RectTransform startCellRect = startCellView.GetComponent<RectTransform>();

            Vector2 highlightSize = new Vector2(
                itemSize.x * _cellSize.x,
                itemSize.y * _cellSize.y
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