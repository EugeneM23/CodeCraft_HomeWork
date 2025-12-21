using Inventories;

public class EquipmentAudioController
{
    private readonly Equipment _equipment;
    private readonly AudioPlayer _audioPlayer;

    public EquipmentAudioController(Equipment equipment)
    {
        _equipment = equipment;
        _audioPlayer = AudioPlayer.Instance;

        OnEnable();
    }

    private void OnEnable()
    {
        _equipment.WeaponSlot.OnItemAdded += OnItemAddedToSlot;
        _equipment.WeaponSlot.OnItemRemoved += OnItemRemovedFromSlot;
        
        _equipment.ArmorSlot.OnItemAdded += OnItemAddedToSlot;
        _equipment.ArmorSlot.OnItemRemoved += OnItemRemovedFromSlot;
    }

    private void OnDisable()
    {
        _equipment.WeaponSlot.OnItemAdded -= OnItemAddedToSlot;
        _equipment.WeaponSlot.OnItemRemoved -= OnItemRemovedFromSlot;
        
        _equipment.ArmorSlot.OnItemAdded -= OnItemAddedToSlot;
        _equipment.ArmorSlot.OnItemRemoved -= OnItemRemovedFromSlot;
    }

    private void OnItemRemovedFromSlot() => _audioPlayer.PlaySlotRemove();

    private void OnItemAddedToSlot() => _audioPlayer.PlaySlotAdd();
}