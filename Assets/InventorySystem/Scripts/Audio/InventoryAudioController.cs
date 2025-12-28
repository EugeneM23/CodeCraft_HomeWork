using Inventories;
using UnityEngine;

public class InventoryAudioController
{
    private readonly Inventory _inventory;
    private readonly AudioPlayer _audioPlayer;

    public InventoryAudioController(Inventory inventory)
    {
        _inventory = inventory;
        _audioPlayer = AudioPlayer.Instance;

        OnEnable();
    }

    private void OnEnable()
    {
        _inventory.OnAdded += OnItemAddedToInventory;
        _inventory.OnRemoved += OnItemRemovedInventory;
        _inventory.OnStackIncreased += OnStackIncreased;
        _inventory.OnStackDecreased += OnStackDecreased;
    }

    private void OnDisable()
    {
        _inventory.OnAdded -= OnItemAddedToInventory;
        _inventory.OnRemoved -= OnItemRemovedInventory;
        _inventory.OnStackIncreased -= OnStackIncreased;
        _inventory.OnStackDecreased -= OnStackDecreased;
    }

    private void OnStackDecreased(ItemInstance _) => _audioPlayer.PlayStackDecreased();

    private void OnStackIncreased(ItemInstance _) => _audioPlayer.PlayStackIncreased();

    private void OnItemRemovedInventory(ItemInstance item)
    {
        if (item.itemData.ItemAudioData.StartDrag == null) return;

        _audioPlayer.Play(item.itemData.ItemAudioData.StartDrag);
    }

    private void OnItemAddedToInventory(ItemInstance item)
    {
        if (item.itemData.ItemAudioData.DropToInventory == null) return;

        _audioPlayer.Play(item.itemData.ItemAudioData.DropToInventory);
    }
}