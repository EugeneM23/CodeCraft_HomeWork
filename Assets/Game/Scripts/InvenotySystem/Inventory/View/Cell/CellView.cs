using UnityEngine;

namespace Inventories
{
    public class CellView : MonoBehaviour
    {
        private InventoryPresenter _presenter;
        private GameObject _itemVisual;

        public void SetPresenter(InventoryPresenter presenter)
        {
            _presenter = presenter;
        }

        public InventoryPresenter Presenter => _presenter;

        public Vector2Int MatrixPosition;
        public Item Item;
        
        /// <summary>
        /// Визуальное представление предмета в этой ячейке (GameObject с иконкой).
        /// Устанавливается при создании предмета в InventoryView.
        /// </summary>
        public GameObject ItemVisual
        {
            get => _itemVisual;
            set => _itemVisual = value;
        }
    }
}