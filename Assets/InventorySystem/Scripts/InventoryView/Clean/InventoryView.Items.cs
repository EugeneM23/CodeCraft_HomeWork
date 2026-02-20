using System;
using UnityEngine;

namespace Inventories
{
    public partial class InventoryView
    {
        private void DisplayItems()
        {
            foreach (var kvp in _presenter.GetItems())
                CreateItem(kvp.Key, kvp.Value);
        }

        private void CreateItem(Item item, Vector2Int[] positions)
        {
            // Связываем ячейки с предметом
            foreach (var pos in positions)
            {
                _cells[pos.x, pos.y].Item = item;
            }

            // Создаем визуальный объект предмета
            var itemObject = _container.InstantiatePrefab(_inventoryItemPrefab, _gridContainer);

            // Настраиваем размер предмета на основе его данных
            var itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            // Позиционируем предмет в первой занятой ячейке
            var firstCell = _cells[positions[0].x, positions[0].y];
            itemRect.anchoredPosition = firstCell.GetComponent<RectTransform>().anchoredPosition;

            // Инициализируем визуальное представление
            var itemView = itemObject.GetComponent<InventoryItemView>();
            itemView.Setup(item.itemData);

            // Сохраняем ссылку на созданный предмет
            _items[item.ID] = itemObject;
        }

        private void RemoveItem(Item item, Vector2Int[] positions)
        {
            if (_items.ContainsKey(item.ID))
            {
                Destroy(_items[item.ID]);
                _items.Remove(item.ID);
            }

            foreach (var pos in positions)
                _cells[pos.x, pos.y].Item = null;
        }

        private void ClearItemsVisual()
        {
            foreach (var item in _items)
                Destroy(item.Value);
        }

        private void Reorganize()
        {
            ClearItemsVisual();
            DisplayItems();
        }
    }
}