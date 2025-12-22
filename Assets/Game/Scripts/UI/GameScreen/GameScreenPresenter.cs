using System;
using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameScreenView _view;
        [SerializeField] private InventoryFactory _inventoryFactory;
        [SerializeField] private TestCharacter _testCharacter;
        [SerializeField] private DragFSM _dragFSM;

        private InventoryPresenter _presenter;

        private void Start()
        {
            InventoryInstaller installer = _view.CreateInventory();
            installer.gameObject.SetActive(false);

            installer.Initialize(_inventoryFactory, _dragFSM);
            _presenter = installer.Presenter;
            _testCharacter.SetInventory(installer.Inventory);
            _dragFSM.SetMainInventory(installer.Inventory);
            installer.Inventory.Owner = _testCharacter;
        }

        private void OnEnable() => _view.OnInventoryButtonClicked += OpenInventory;

        private void OnDisable() => _view.OnInventoryButtonClicked -= OpenInventory;

        private void OpenInventory()
        {
            if (_presenter.IsOpen)
                _presenter.Hide();
            else
                _presenter.Show(_testCharacter.Inventory);
        }
    }
}