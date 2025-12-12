using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;

    private CellView[,] _inventoryMatrix;
    private IInventoryCollection _presenter;
    private Vector2Int _gridPosition;
    private Vector2Int _itemMatrixPosition;
    private InventoryItem _inventoryItem;
    public CellView[,] InventoryMatrix => _inventoryMatrix;
    public Vector2Int GridPosition => _gridPosition;
    public InventoryItem InventoryItem
    {
        get => _inventoryItem;
        set => _inventoryItem = value;
    }

    public IInventoryCollection Presenter => _presenter;

    public void Construct(IInventoryCollection presenter, Vector2Int gridPosition, CellView[,] cells)
    {
        _inventoryMatrix = cells;
        _presenter = presenter;
        _gridPosition = gridPosition;
    }

    public void Construct(InventoryItem inventoryItem, Vector2Int matrixPos)
    {
        _inventoryItem = inventoryItem;
        _itemMatrixPosition = matrixPos;
    }

    public void Clear()
    {
        _inventoryItem = null;
        _itemMatrixPosition = default;
    }

    public void Highlight(bool isCorrect) => _backGround.sprite = isCorrect ? _freeSprite : _errorSprite;

    public void UnHighlight() => _backGround.sprite = _emptySprite;
}