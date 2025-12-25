using Inventories;
using UnityEngine;

[CreateAssetMenu(fileName = "ArmorSet", menuName = "InventoryItem/ArmorSet/ArmorSet")]
public class ArmorSet : ScriptableObject
{
    [SerializeField] private Mesh _mesh;
    [SerializeField] private ItemType _itemType;
    public Mesh Mesh => _mesh;
    public ItemType ItemType => _itemType;
}