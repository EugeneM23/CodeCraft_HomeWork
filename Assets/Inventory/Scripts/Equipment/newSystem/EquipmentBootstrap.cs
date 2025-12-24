using System;
using Game.Scripts.UI.Equipment.Game.Equipment.Presenter;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using UnityEngine;

namespace Game.Scripts.UI.Equipment.Game.Equipment
{
    public class EquipmentBootstrap : MonoBehaviour
    {
        [SerializeField] private EquipmentPanelView _view;
        private ItemConsumer _consumer;

        private EquipmentPresenter _presenter;

        public void Initialize(ItemConsumer consumer)
        {
            _consumer = consumer;
            _presenter = new EquipmentPresenter(_view, _consumer.Inventory);
            //_presenter.Subscribe();
            _consumer.SetEquipment(_presenter);
        }

        private void OnEnable()
        {
            _presenter.Subscribe();
        }

        private void OnDisable()
        {
            _presenter.UnSubscribe();
        }
    }
}