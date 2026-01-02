using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.Equipment.Game.Equipment
{
    public class EquipmentBootstrap : MonoBehaviour
    {
        [SerializeField] private EquipmentPresenter _presenter;
        [SerializeField] private InventoryView _inventory;

        public EquipmentPresenter Presenter => _presenter;

        public void Initialize(InventoryPresenter inventoryPresenter, ItemConsumer consumer)
        {
            _presenter.Initialize(_inventory, inventoryPresenter);
            consumer.SetEquipment(_presenter);
        }
    }
}