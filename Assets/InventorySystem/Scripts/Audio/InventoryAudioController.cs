using Inventories;
using UnityEngine;

public class InventoryAudioController : MonoBehaviour
{
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;

    private InventoryPresenter _inventoryPresenter;
    private AudioPlayer _audioPlayer;
    private Inventory _inventory;

    public void Initialize(InventoryPresenter presenter)
    {
        _inventoryPresenter = presenter;
        _inventory = presenter.GetInventory();
        _audioPlayer = AudioPlayer.Instance;

        Subscribe();
    }

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (_inventoryPresenter == null) return;

        //_inventory.OnAdded += OnItemAddedToInventory;
        //_inventory.OnRemoved += OnItemRemovedFromInventory;

        _inventoryPresenter.OnShow += PlayOpenSound;
        _inventoryPresenter.OnHide += PlayCloseSound;
    }

    private void Unsubscribe()
    {
        if (_inventoryPresenter == null) return;

        //_inventory.OnAdded -= OnItemAddedToInventory;
        //_inventory.OnRemoved -= OnItemRemovedFromInventory;

        _inventoryPresenter.OnShow -= PlayOpenSound;
        _inventoryPresenter.OnHide -= PlayCloseSound;
    }

    private void OnItemAddedToInventory(Item item)
    {
        _audioPlayer.Play(item?.itemData.ItemAudioData.DropToInventory);
    }

    private void OnItemRemovedFromInventory(Item item)
    {
        _audioPlayer.Play(item?.itemData.ItemAudioData.StartDrag);
    }

    private void PlayOpenSound()
    {
        if (_openSound != null)
            _audioPlayer.Play(_openSound);
    }

    private void PlayCloseSound()
    {
        if (_closeSound != null)
            _audioPlayer.Play(_closeSound);
    }
}