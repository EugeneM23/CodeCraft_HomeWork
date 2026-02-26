using System;
using UnityEngine;

namespace Inventories
{
    public class Item
    {
        public string ID { get; private set; }
        public ItemSettings Settings { get; }
        public Vector2Int GridPosition { get; private set; }

        public Item(ItemSettings settings, Vector2Int position)
        {
            ID = Guid.NewGuid().ToString();
            Settings = settings;
            GridPosition = position;
        }

        public void SetGridPosition(Vector2Int newPosition)
        {
            GridPosition = newPosition;
        }
    }
}