using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToInventoryHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем ячейку инвентаря под курсором при отпускании предмета
            var cell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventoryCell>();

            // Если курсор не над ячейкой инвентаря, передаем обработку дальше
            if (cell == null)
                return false;

            // Вычисляем целевую позицию предмета (левый верхний угол) с учетом смещения клика
            Vector2Int targetPosition = cell.MatrixPosition - dragContext.ClickOffset;
            
            // Пытаемся добавить предмет в инвентарь
            return cell.Presenter.AddItem(dragContext.Item, targetPosition);
        }
    }
}