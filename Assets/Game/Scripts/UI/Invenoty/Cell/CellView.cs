using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;

    private IInventoryCollection _presenter;
    private Vector2Int _gridPosition;
    private Vector2Int _itemMatrixPosition;
    private InventoryItem _inventoryItem;
    public Vector2Int ItemMatrixPosition => _itemMatrixPosition;
    public Vector2Int GridPosition => _gridPosition;
    public InventoryItem InventoryItem => _inventoryItem;
    public IInventoryCollection Presenter => _presenter;

    public void Construct(IInventoryCollection presenter, Vector2Int gridPosition)
    {
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