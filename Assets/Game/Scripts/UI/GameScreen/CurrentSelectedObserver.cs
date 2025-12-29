using System;
using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.GameScreen
{
    public class CurrentSelectedObserver : MonoBehaviour
    {
        [SerializeField] private ItemConsumer itemConsumer;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private CharacterSelector _characterSelector;
        [SerializeField] private GameScreenPresenter _presenter;

        private void Start()
        {
            _characterSelector.OnUnitChanged += OnUnitChanged;
            _presenter.HideInventoryButton();
        }

        private void OnUnitChanged(Entity entity)
        {
            if (entity != null)
            {
                _presenter.ShowInventoryButton();
                _presenter.SetCurrentUnit(entity);
                _dragFSM.SetMainInventory(entity.InventoryPresenter);
                itemConsumer.SetInventory(entity.InventoryPresenter);
            }
            else
                _presenter.HideInventoryButton();
        }
    }
}