using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class CharacterEquipment : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer _headRenderer;
    [SerializeField] private SkinnedMeshRenderer _bodyRenderer;
    [SerializeField] private SkinnedMeshRenderer _bootsRenderer;
    [SerializeField] private SkinnedMeshRenderer _legsRenderer;
    [SerializeField] private SkinnedMeshRenderer _handsRenderer;
    [SerializeField] private MeshFilter _weaponFilter;
    [SerializeField] private MeshFilter _shieldFilter;

    [SerializeField] private Mesh _defaultHead;
    [SerializeField] private Mesh _defaultBody;
    [SerializeField] private Mesh _defaultLegs;
    [SerializeField] private Mesh _defaultBoots;
    [SerializeField] private Mesh _defaultHands;

    private Dictionary<ItemType, SkinnedMeshRenderer> _renderers;
    private Dictionary<ItemType, Mesh> _defaults;

    private void Awake()
    {
        _renderers = new Dictionary<ItemType, SkinnedMeshRenderer>
        {
            [ItemType.Head] = _headRenderer,
            [ItemType.Body] = _bodyRenderer,
            [ItemType.Legs] = _legsRenderer,
            [ItemType.Boots] = _bootsRenderer,
            [ItemType.Hands] = _handsRenderer
        };

        _defaults = new Dictionary<ItemType, Mesh>
        {
            [ItemType.Head] = _defaultHead,
            [ItemType.Body] = _defaultBody,
            [ItemType.Legs] = _defaultLegs,
            [ItemType.Boots] = _defaultBoots,
            [ItemType.Hands] = _defaultHands
        };
    }

    public void Equip(Item item)
    {
        var itemType = item.Settings.ItemType;
        var mesh = item.Settings.Mesh;

        if (itemType == ItemType.Weapon)
            _weaponFilter.mesh = mesh;
        else if (itemType == ItemType.Shield)
            _shieldFilter.mesh = mesh;
        else
            _renderers[itemType].sharedMesh = mesh;
    }

    public void Unequip(Item item)
    {
        var itemType = item.Settings.ItemType;

        if (itemType == ItemType.Weapon)
            _weaponFilter.mesh = null;
        else if (itemType == ItemType.Shield)
            _shieldFilter.mesh = null;
        else
            _renderers[itemType].sharedMesh = _defaults[itemType];
    }
}