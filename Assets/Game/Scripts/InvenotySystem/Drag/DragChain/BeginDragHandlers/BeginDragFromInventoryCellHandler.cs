using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    /// <summary>
    /// Обработчик начала перетаскивания предмета из ячейки инвентаря.
    /// </summary>
    public class BeginDragFromInventoryCellHandler : IBeginDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем ячейку, на которую кликнули
            GameObject cellObject = eventData.pointerPressRaycast.gameObject;

            if (!cellObject.TryGetComponent(out CellView cell))
                return false;

            // Если в ячейке нет предмета - выходим
            if (cell.Item == null)
                return false;

            var item = cell.Item;

            // Получаем ItemDragHandler из родительского объекта (InventoryView)
            var handler = cell.GetComponentInParent<ItemDragHandler>();

            // Вычисляем начальную позицию предмета в сетке
            Vector2Int itemStartPosition = cell.Presenter.GetItemPosition(item.ID);
            
            // Вычисляем смещение клика относительно начала предмета
            Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;
            
            // Вычисляем пиксельное смещение для правильного позиционирования под курсором
            Vector2 dragOffset = (Vector2)cell.ItemVisual.transform.position - eventData.position;

            // Инициализируем контекст перетаскивания
            dragContext.BeginDrag(item, itemStartPosition, clickOffset, dragOffset, cell, null);

            // Создаем визуальное представление под курсором
            handler.SetupDraggableImage(item);

            // Удаляем предмет из инвентаря (вернется при дропе)
            cell.Presenter.RemoveItem(item);

            return true;
        }
    }
}