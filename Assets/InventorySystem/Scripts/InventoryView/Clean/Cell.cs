using UnityEngine;
using Zenject;

namespace Inventories
{
    public class Cell : MonoBehaviour
    {
        [Inject] private readonly InventoryAdapter _adapter;
        public InventoryAdapter Adapter => _adapter;
        public Vector2Int MatrixPosition { get; private set; }
        public Item Item { get; private set; }

        public void Construct(Vector2Int matrixPosition)
        {
            MatrixPosition = matrixPosition;
        }

        public void SetItem(Item item) => Item = item;
    }
}