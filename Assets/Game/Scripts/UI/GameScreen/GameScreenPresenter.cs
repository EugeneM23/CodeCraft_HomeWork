using System;
using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;

namespace Inventories
{
    public class GameScreenPresenter : MonoBehaviour
    {
        public event Action<InventoryPresenter> OnInventoryCreated;

        [SerializeField] private GameScreenView _view;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private CharacterSelector _characterSelector;

        private Entity _currentSelectedUnit;

        private void Start()
        {
            _characterSelector.OnUnitChanged += OnUnitChanged;
            _view.Hide();
        }

        private void OnEnable() => _view.OnInventoryButtonClicked += ToggleInventory;

        private void OnDisable()
        {
            _view.OnInventoryButtonClicked -= ToggleInventory;
            _characterSelector.OnUnitChanged -= OnUnitChanged;
        }

        private void OnUnitChanged(Entity entity)
        {
            if (entity != null)
            {
                SetCurrentUnit(entity);
                _view.Show();

                if (entity.InventoryPresenter != null)
                    _dragFSM.SetMainInventory(entity.InventoryPresenter);
            }
            else
            {
                _view.Hide();
            }
        }

        private void ToggleInventory()
        {
            if (_currentSelectedUnit.InventoryPresenter == null)
            {
                CreateInventory();
                OnInventoryCreated?.Invoke(_currentSelectedUnit.InventoryPresenter);
            }

            _currentSelectedUnit.InventoryPresenter.Show();
        }

        private void CreateInventory()
        {
            InventoryBootstrap bootstrap = _view.CreateInventory(_currentSelectedUnit.InventoryPrefab);
            bootstrap.Construct(_factory, _dragFSM, _currentSelectedUnit.ItemConsumer);

            _currentSelectedUnit.InventoryPresenter = bootstrap.Presenter;
            _dragFSM.SetMainInventory(bootstrap.Presenter);
            
            // Bootstrap сам инициализирует экипмент внутри
        }

        private void SetCurrentUnit(Entity entity)
        {
            _currentSelectedUnit = entity;
        }
    }
}