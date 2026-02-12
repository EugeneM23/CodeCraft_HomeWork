using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject _cellPrefab;
        [SerializeField] private Vector2Int _cellSize = new(64, 64);
        [SerializeField] private RectTransform _gridContainer;

        [Inject] private readonly InventoryAdapter _adapter;

        private void Start()
        {
            CreateGrid(_cellSize.x, _cellSize.y);
        }

        public void Show()
        {
            
        }

        public void Hide()
        {
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void CreateGrid(int width, int height)
        {
            for (int y = 0; y < _adapter.Height; y++)
            {
                for (int x = 0; x < _adapter.Widht; x++)
                    CreateCell(x, y);
            }
        }

        private void CreateCell(int x, int y)
        {
            var cell = Instantiate(_cellPrefab, this._gridContainer);
            RectTransform rect = cell.GetComponent<RectTransform>();
            rect.sizeDelta = _cellSize;
            rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);
        }
    }
}