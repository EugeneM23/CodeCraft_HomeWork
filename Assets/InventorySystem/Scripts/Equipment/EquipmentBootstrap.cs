using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Scripts.UI.Equipment.Game.Equipment
{
    public class EquipmentBootstrap : MonoBehaviour
    {
        [SerializeField] private EquipmentPresenter _presenter;

        public EquipmentPresenter Presenter => _presenter;

        public void Initialize(ItemConsumer consumer = null)
        {
            _presenter.Initialize(consumer.Inventory);
            consumer.SetEquipment(_presenter);
        }
    }
}