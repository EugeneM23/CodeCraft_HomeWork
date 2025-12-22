using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class EquipmentInstaller : SerializedMonoBehaviour
{
    [Header("Equipment Settings")] [SerializeField]
    private Equipment _equipment;

    private EquipmentAudioController _equipmentAudioController;

    public Equipment Equipment => _equipment;
    public EquipmentPresenter Presenter { get; set; }

    public void Initialize(IItemConsumer consumer)
    {
        _equipmentAudioController = new EquipmentAudioController(_equipment);
        gameObject.SetActive(false);
    }
}