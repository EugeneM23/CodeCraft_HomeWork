using System;
using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameScreenView _view;
        private InventoryPresenter _presenter;

        private void Start() => _presenter = _view.Initialize();

        private void OnEnable() => _view.OnInventoryButtonClicked += OpenInventory;

        private void OnDisable() => _view.OnInventoryButtonClicked -= OpenInventory;

        private void OpenInventory()
        {
            if (_presenter.IsOpen)
                _presenter.OnHide();
            else
                _presenter.OnShow();
        }
    }
}