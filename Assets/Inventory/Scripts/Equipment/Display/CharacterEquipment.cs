using System.Collections.Generic;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class CharacterEquipment : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer _headMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _bodyMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _bootsMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _gauntletsMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _legMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _handsMeshRenderer;

    [SerializeField] private MeshFilter _weaponMeshRenderer01;
    [SerializeField] private MeshFilter _weaponMeshRenderer02;

    [SerializeField] private Mesh _headMesh;
    [SerializeField] private Mesh _bodyMesh;
    [SerializeField] private Mesh _legMesh;
    [SerializeField] private Mesh _bootsMesh;
    [SerializeField] private Mesh _handsMesh;

    private readonly Dictionary<ItemType, SkinnedMeshRenderer> _items = new();
    private readonly Dictionary<ItemType, Mesh> _nakedParts = new();

    private void Start()
    {
        _items[ItemType.Head] = _headMeshRenderer;
        _items[ItemType.Body] = _bodyMeshRenderer;
        _items[ItemType.Legs] = _legMeshRenderer;
        _items[ItemType.Boots] = _bootsMeshRenderer;
        _items[ItemType.Hands] = _handsMeshRenderer;

        _nakedParts[ItemType.Head] = _headMesh;
        _nakedParts[ItemType.Body] = _bodyMesh;
        _nakedParts[ItemType.Legs] = _legMesh;
        _nakedParts[ItemType.Boots] = _bootsMesh;
        _nakedParts[ItemType.Hands] = _handsMesh;
    }

    public void Equip(ItemInstance item, EquipmentSlotView slot)
    {
        Debug.Log("Equip");
        if (item.itemData.ItemType == ItemType.Shield)
        {
            _weaponMeshRenderer02.mesh = item.itemData.Mesh;
            return;
        }

        if (item.itemData.ItemType == ItemType.Weapon)
        {
            _weaponMeshRenderer01.mesh = item.itemData.Mesh;
            return;
        }

        _items[item.itemData.ItemType].sharedMesh = item.itemData.Mesh;
    }

    public void UnEquip(ItemInstance item, EquipmentSlotView slot)
    {
        if (item.itemData.ItemType == ItemType.Shield)
        {
            _weaponMeshRenderer02.mesh = null;
            return;
        }

        if (item.itemData.ItemType == ItemType.Weapon)
        {
            _weaponMeshRenderer01.mesh = null;
            return;
        }

        _items[item.itemData.ItemType].sharedMesh = _nakedParts[item.itemData.ItemType];
    }
}