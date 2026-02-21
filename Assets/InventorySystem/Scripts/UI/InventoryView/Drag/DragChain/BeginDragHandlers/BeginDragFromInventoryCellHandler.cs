using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class BeginDragFromInventoryCellHandler : IBeginDraghendler
    {
        private readonly InventoryView _view;
        private readonly InventoryPresenter _presenter;

        public BeginDragFromInventoryCellHandler(InventoryView view, InventoryPresenter presenter)
        {
            _view = view;
            _presenter = presenter;
        }

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем ячейку под курсором
            var cell = eventData.pointerPressRaycast.gameObject.GetComponent<InventoryCell>();
            if (cell == null || cell.Item == null)
                return false;

            var item = cell.Item;
            var items = _view.GetItems();

            // Вычисляем позицию предмета в инвентаре
            Vector2Int itemStartPosition = _presenter.GetItemPosition(item.ID);

            // Вычисляем смещение клика относительно левого верхнего угла предмета
            Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;

            // Вычисляем смещение курсора относительно визуальной позиции предмета
            Vector2 dragOffset = (Vector2)items[item.ID].transform.position - eventData.position;

            // Начинаем драг с сохранением всех смещений
            dragContext.BeginDrag(item, itemStartPosition, clickOffset, dragOffset);

            // Настраиваем визуальное отображение драгаемого предмета
            _view.SetupDraggableImage(item);

            // Удаляем предмет из инвентаря на время драга
            _presenter.RemoveItem(item);

            return true;
        }
    }
}