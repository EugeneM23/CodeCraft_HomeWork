using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;

    private CellView[,] _inventoryMatrix;
    private InventoryPresenter _presenter;
    private Vector2Int _gridPosition;
    public CellView[,] InventoryMatrix => _inventoryMatrix;
    public Vector2Int GridPosition => _gridPosition;
    public InventoryItem InventoryItem { get; set; }

    public InventoryPresenter Presenter => _presenter;

    public void Construct(InventoryPresenter presenter, Vector2Int gridPosition, CellView[,] cells)
    {
        _inventoryMatrix = cells;
        _presenter = presenter;
        _gridPosition = gridPosition;
    }

    public void Highlight(bool isCorrect) => _backGround.sprite = isCorrect ? _freeSprite : _errorSprite;

    public void UnHighlight() => _backGround.sprite = _emptySprite;
}