using Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragFromEquipmentSlotHandler : IBeginDraghendler
    {
        [Inject] private readonly InventoryView _view;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем слот экипировки под курсором
            EquipmentSlot slotView = eventData.pointerPressRaycast.gameObject.GetComponent<EquipmentSlot>();

            // Проверяем что слот существует и не пустой
            if (slotView == null || slotView.IsEmpty)
                return false;

            // Снимаем предмет со слота экипировки
            //Item item = slotView.Presenter.UnEquip(slotView.ItemType);

            Item item = slotView.UnEquip();

            // Вычисляем смещение курсора относительно центра слота
            Vector2 dragOffset = (Vector2)slotView.transform.position - eventData.position;

            // Начинаем драг без матричных смещений (предмет из экипировки не имеет позиции в сетке)
            dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset, null, slotView);

            // Настраиваем визуальное отображение драгаемого предмета
            Debug.Log(item.ID);
            _view.SetupDraggableImage(item);

            return true;
        }
    }
}