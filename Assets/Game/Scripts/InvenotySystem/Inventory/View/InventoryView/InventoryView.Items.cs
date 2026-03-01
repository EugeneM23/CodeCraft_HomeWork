using System;
using UnityEngine;

namespace Inventories
{
    public partial class InventoryView
    {
        private void CreateAllItems()
        {
            foreach (var kvp in _presenter.GetItems())
                CreateItem(kvp.Key, kvp.Value);
        }

        private void CreateItem(Item item, Vector2Int[] positions)
        {
            // Создаем визуальный объект предмета
            var itemObject = _container.InstantiatePrefab(_inventoryItemPrefab, _gridContainer);

            // Настраиваем размер предмета на основе его данных
            var itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(
                item.Settings.Size.x * _cellSize.x,
                item.Settings.Size.y * _cellSize.y);

            // Позиционируем предмет в первой занятой ячейке
            var firstCell = _cells[positions[0].x, positions[0].y];
            itemRect.anchoredPosition = firstCell.GetComponent<RectTransform>().anchoredPosition;

            // Инициализируем визуальное представление
            var itemView = itemObject.GetComponent<InventoryItemView>();
            itemView.Setup(item.Settings);

            // Сохраняем ссылку на созданный предмет
            _items[item.ID] = itemObject;

            // Связываем ячейки с предметом и устанавливаем визуальный объект
            foreach (var pos in positions)
            {
                _cells[pos.x, pos.y].Item = item;
                _cells[pos.x, pos.y].ItemVisual = itemObject; // Устанавливаем визуальный объект
            }
        }

        private void RemoveItem(Item item, Vector2Int[] positions)
        {
            if (_items.ContainsKey(item.ID))
            {
                Destroy(_items[item.ID]);
                _items.Remove(item.ID);
            }

            foreach (var pos in positions)
            {
                _cells[pos.x, pos.y].Item = null;
                _cells[pos.x, pos.y].ItemVisual = null; // Очищаем ссылку на визуальный объект
            }
        }

        private void DestroyAllItems()
        {
            foreach (var item in _items)
                Destroy(item.Value);
        }

        private void Reorganize()
        {
            DestroyAllItems();
            CreateAllItems();
        }
    }
}