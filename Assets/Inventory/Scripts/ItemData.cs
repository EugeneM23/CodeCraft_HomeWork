using UnityEngine;

namespace Inventories
{
    [CreateAssetMenu(fileName = "ItemData_", menuName = "Inventory/ItemData")]
    public sealed class ItemData : ScriptableObject
    {
        [field: SerializeField] public string Name;
        [field: SerializeField] public Sprite Icon;
        [field: SerializeField] public Vector2Int Size;
        [field: SerializeField] public ItemType ItemType;
        [field: SerializeField] public GameObject SpawnPrefab;

        [field: SerializeField] public bool CanStack;
        [field: SerializeField] public int MaxStackQuantity;
        [field: SerializeField] public int CurrentStackQuantity;
    }
}