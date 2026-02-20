using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class Cell : MonoBehaviour
    {
        [Inject] private readonly InventoryAdapterN _adapter;

        public InventoryAdapterN Adapter => _adapter;

        public Vector2Int MatrixPosition;
        public Item Item;
    }
}