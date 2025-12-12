using UnityEngine;
using UnityEngine.Serialization;

namespace Inventories
{
    [System.Serializable]
    public class ItemInstance
    {
        public string uniqueId;
        public ItemData itemData;

        public ItemInstance(ItemData type)
        {
            itemData = type;
            uniqueId = System.Guid.NewGuid().ToString();
        }

        public Vector2Int GridPosition { get; set; }
    }
}