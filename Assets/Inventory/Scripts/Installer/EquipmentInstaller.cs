using Sirenix.OdinInspector;
using UnityEngine;

public class EquipmentInstaller : SerializedMonoBehaviour
{
    [Header("Equipment Settings")] 
    [SerializeField] private Equipment _equipment;

    private EquipmentAudioController _equipmentAudioController;

    public Equipment Equipment => _equipment;

    public void Initialize(IItemConsumer consumer)
    {
        // Initialize audio controller
        _equipmentAudioController = new EquipmentAudioController(_equipment);
    }
}