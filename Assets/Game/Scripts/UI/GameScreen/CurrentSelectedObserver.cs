using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.GameScreen
{
    public class CurrentSelectedObserver : MonoBehaviour
    {
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private CharacterSelector _characterSelector;
        [SerializeField] private GameScreenPresenter _presenter;
        [SerializeField] private Chest.Chest[] _chests;

        private void Start()
        {
            _characterSelector.OnUnitChanged += OnUnitChanged;
            _presenter.HideInventoryButton();
            _presenter.OnInventoryCreated += OnInventoryCreated;
        }

        private void OnUnitChanged(Entity entity)
        {
            if (entity != null)
            {
                _presenter.SetCurrentUnit(entity);
                _presenter.ShowInventoryButton();
                
                if (entity.InventoryPresenter != null)
                    _dragFSM.SetMainInventory(entity.InventoryPresenter);
                
                UpdateChests(entity);
            }
            else
            {
                _presenter.HideInventoryButton();
            }
        }

        private void OnInventoryCreated(InventoryPresenter playerInventory)
        {
            _dragFSM.SetMainInventory(playerInventory);
            UpdateChests(_presenter.GetCurrentUnit());
        }

        private void UpdateChests(Entity entity)
        {
            foreach (var chest in _chests)
            {
                chest.SetCurrentEntity(entity);
            }
        }
    }
}