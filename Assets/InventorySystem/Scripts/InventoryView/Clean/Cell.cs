using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryCell : MonoBehaviour, IItemCell
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public InventoryPresenter Presenter => _presenter;

        public Vector2Int MatrixPosition;
        public Item Item;
        
        public void AddItem(Item item)
        {
            
        }
    }

    public interface IItemCell
    {
        void AddItem(Item item);
    }

    public class EquipmentCell : IItemCell
    {
        public void AddItem(Item item)
        {
            
        }
    }
}