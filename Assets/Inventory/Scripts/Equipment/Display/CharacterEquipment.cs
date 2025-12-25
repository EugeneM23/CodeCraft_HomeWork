using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterEquipment : MonoBehaviour
{
    [SerializeField] private Transform _root;

    [Header("Armor Mesh Renderers")] [SerializeField]
    private SkinnedMeshRenderer _headMeshRenderer;

    [SerializeField] private SkinnedMeshRenderer _bodyMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _bootsMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _gauntletsMeshRenderer;
    [SerializeField] private SkinnedMeshRenderer _legMeshRenderer;

    [Header("Armor Set")] [SerializeField] private ArmorSet _armorSet;
    [SerializeField] private ArmorSet _helmet;
    [SerializeField] private ArmorSet _head;
    [SerializeField] private ArmorSet _nakedSet;

    [Header("Weapon")] [SerializeField] private Transform _weaponSlot;
    [SerializeField] private GameObject _weaponPrefab;

    private GameObject _currentWeapon;

    private readonly Dictionary<ItemType, SkinnedMeshRenderer> _items = new();

    private void Start()
    {
        _items[ItemType.Armor] = _bodyMeshRenderer;
        _items[ItemType.Helmet] = _headMeshRenderer;
    }

    public void EquipArmor()
    {
        if (_armorSet == null)
            return;

        _items[_armorSet.ItemType].sharedMesh = _armorSet.Mesh;
    }

    public void UnEquipArmor()
    {
        _items[_armorSet.ItemType].sharedMesh = _nakedSet.Mesh;
    }

    public void EquipHead()
    {
        Debug.Log("Equip Head");
        _items[_helmet.ItemType].sharedMesh = _helmet.Mesh;
    }

    public void UnEquipHead()
    {
        _items[_head.ItemType].sharedMesh = _head.Mesh;
    }
}