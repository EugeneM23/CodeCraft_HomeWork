using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class InventoryAudioController : MonoBehaviour
{
    [SerializeField] private BackPack _backpack;
    [SerializeField] private AudioPlayer _audioPlayer;
    [SerializeField] private EquipmentSlot _equipmentSlot;

    private void OnEnable()
    {
        _backpack.Inventory.OnAdded += OnItemAddedToInventory;
        _backpack.Inventory.OnRemoved += OnItemRemovedInventory;
        _equipmentSlot.OnItemAdded += OnItemAddedToSlot;
        _equipmentSlot.OnItemRemoved += OnItemremovedFromToSlot;
    }

    private void OnItemremovedFromToSlot() => _audioPlayer.PlaySlotRemove();

    private void OnItemAddedToSlot() => _audioPlayer.PlaySlotAdd();

    private void OnItemRemovedInventory(ItemInstance _) => _audioPlayer.PlayInventoryRemove();

    private void OnItemAddedToInventory(ItemInstance _) => _audioPlayer.PlayInventoryAdd();
}