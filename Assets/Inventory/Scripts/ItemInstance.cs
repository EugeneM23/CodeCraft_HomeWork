using UnityEngine.Serialization;

namespace Inventories
{
    [System.Serializable]
    public class ItemInstance
    {
        public string uniqueId;
        public Item Item;

        public ItemInstance(Item type)
        {
            Item = type;
            uniqueId = System.Guid.NewGuid().ToString();
        }
    }
}