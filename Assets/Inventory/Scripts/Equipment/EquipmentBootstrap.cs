using System;
using Game.Scripts.UI.Equipment.Game.Equipment;
using Game.Scripts.UI.Equipment.Game.Equipment.Presenter;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.UI.Equipment.Game.Equipment
{
    public class EquipmentBootstrap : MonoBehaviour
    {
        [SerializeField] private EquipmentPanelView _view;
        [SerializeField] private EquipmentController _controller;

        private ItemConsumer _consumer;
        private EquipmentPresenter _presenter;
        public EquipmentPresenter Presenter => _presenter;

        public void Initialize(ItemConsumer consumer)
        {
            _consumer = consumer;
            _presenter = new EquipmentPresenter(_view, _consumer.Inventory);
            _consumer.SetEquipment(_presenter);
            _consumer.SetController(_controller);
        }

        private void OnEnable()
        {
            _presenter.Subscribe();
        }

        private void OnDisable()
        {
            _presenter.Unsubscribe();
        }
    }
}