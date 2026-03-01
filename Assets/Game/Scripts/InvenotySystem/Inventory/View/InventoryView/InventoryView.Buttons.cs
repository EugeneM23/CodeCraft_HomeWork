using System;
using UnityEngine;
using UnityEngine.UI;

namespace Inventories
{
    public partial class InventoryView
    {
      
        
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _reorganizeButton;
        [SerializeField] private Button _openEquipment;

        private int _equipmentID;
        private bool _isEquipmentEnabled = true;

        private void SubscribeButtons()
        {
            _reorganizeButton.onClick.AddListener(HandleReorganize);
            _closeButton.onClick.AddListener(HandleClose);
            _openEquipment.onClick.AddListener(HandleOpenEquipment);
        }

        private void UnsubscribeButtons()
        {
            _reorganizeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.RemoveAllListeners();
            _openEquipment.onClick.RemoveAllListeners();
        }

        private void HandleClose()
        {
            _signalBuss.Fire(new EnableEquipmentSignal
            {
                ID = _equipmentID,
                IsEnable = false
            });

            gameObject.SetActive(false);
        }

        private void HandleReorganize() => _presenter.Reorganize();

        private void HandleOpenEquipment()
        {
            _isEquipmentEnabled = !_isEquipmentEnabled;

            _signalBuss.Fire(new EnableEquipmentSignal
            {
                ID = _equipmentID,
                IsEnable = _isEquipmentEnabled
            });
        }
    }
}