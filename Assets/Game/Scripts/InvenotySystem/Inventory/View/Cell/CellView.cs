using UnityEngine;
using Zenject;

namespace Inventories
{
    public class CellView : MonoBehaviour
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public InventoryPresenter Presenter => _presenter;

        public Vector2Int MatrixPosition;
        public Item Item;
    }
}