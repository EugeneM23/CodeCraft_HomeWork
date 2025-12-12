using UnityEngine;

namespace Inventories
{
    [CreateAssetMenu(fileName = "Item_", menuName = "Inventory/Item")]
    public sealed class Item : ScriptableObject
    {
        [field: SerializeField] public string ItemID;
        [field: SerializeField] public string Name;
        [field: SerializeField] public Sprite Icon;
        [field: SerializeField] public Vector2Int Size;
        [field: SerializeField] public ItemType ItemType;

        public Vector2Int GridPosition { get; set; }
    }
}