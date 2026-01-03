using Inventories;
using UnityEngine;

public class EquipmentAudioController : MonoBehaviour
{
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;

    private EquipmentPresenter _equipmentPresenter;
    private AudioPlayer _audioPlayer;

    public void Initialize(EquipmentPresenter equipmentPresenter)
    {
        _equipmentPresenter = equipmentPresenter;
        _audioPlayer = AudioPlayer.Instance;

        Subscribe();
    }

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (_equipmentPresenter == null) return;

        foreach (var slot in _equipmentPresenter.GetAllSlots())
        {
            slot.OnEquipped += OnItemEquipped;
            slot.OnUnEquipped += OnItemUnequipped;
        }

        _equipmentPresenter.OnShow += PlayOpenSound;
        _equipmentPresenter.OnHide += PlayCloseSound;
    }

    private void Unsubscribe()
    {
        if (_equipmentPresenter == null) return;

        foreach (var slot in _equipmentPresenter.GetAllSlots())
        {
            slot.OnEquipped -= OnItemEquipped;
            slot.OnUnEquipped -= OnItemUnequipped;
        }

        _equipmentPresenter.OnShow -= PlayOpenSound;
        _equipmentPresenter.OnHide -= PlayCloseSound;
    }

    private void OnItemEquipped(Item item)
    {
        _audioPlayer.Play(item.itemData.ItemAudioData.DropToInventory);
    }

    private void OnItemUnequipped(Item item)
    {
        _audioPlayer.Play(item.itemData.ItemAudioData.DropToInventory);
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