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

    [SerializeField] private Transform _weaponRoot;
    [SerializeField] private Transform _shieldRoot;

    private Dictionary<ItemType, SkinnedMeshRenderer> _renderers;

    private GameObject _weapon;
    private GameObject _shield;

    private void Awake()
    {
        _renderers = new Dictionary<ItemType, SkinnedMeshRenderer>
        {
            [ItemType.Head] = _headRenderer,
            [ItemType.Armor] = _bodyRenderer,
            [ItemType.Legs] = _legsRenderer,
            [ItemType.Boots] = _bootsRenderer,
            [ItemType.Hands] = _handsRenderer
        };
    }

    public void Equip(Item item)
    {
        var itemType = item.Settings.ItemType;

        if (itemType == ItemType.Weapon)
        {
            _weapon = Instantiate(item.Settings.ItemPrefab, _weaponRoot);
            _weapon.transform.position = _weaponRoot.transform.position;
        }
        else if (itemType == ItemType.Shield)
        {
            Destroy(_shield);

            _shield = Instantiate(item.Settings.ItemPrefab, _shieldRoot);
        }
        else
        {
            var mesh = item.Settings.Mesh;
            _renderers[itemType].sharedMesh = mesh;
        }
    }

    public void Unequip(Item item)
    {
        var itemType = item.Settings.ItemType;

        if (itemType == ItemType.Weapon)
        {
            Destroy(_weapon);
        }
        else if (itemType == ItemType.Shield)
        {
            Destroy(_shield);
        }
        else
        {
            _renderers[itemType].sharedMesh = null;
        }
    }
}