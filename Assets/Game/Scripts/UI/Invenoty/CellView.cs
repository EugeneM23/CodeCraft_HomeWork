using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;

    private InventoryPresenter _presenter;
    public Vector2Int _gridPosition;
    public Vector2Int _itemMatrixPosition;
    private Item _item;
    private InventoryItem _inventoryItem;
    public Vector2Int ItemMatrixPosition => _itemMatrixPosition;
    public Vector2Int GridPosition => _gridPosition;
    public InventoryItem InventoryItem => _inventoryItem;
    public InventoryPresenter Presenter => _presenter;
    public Item Item => _item;

    public void Construct(InventoryPresenter presenter, Vector2Int gridPosition)
    {
        _presenter = presenter;
        _gridPosition = gridPosition;
    }

    public void Construct(Item item, InventoryItem inventoryItem, Vector2Int matrixPos)
    {
        _item = item;
        _inventoryItem = inventoryItem;
        _itemMatrixPosition = matrixPos;
    }

    public void Clear()
    {
        _item = null;
        _inventoryItem = null;
        _itemMatrixPosition = default;
    }

    public void Highlight(bool isCorrect) => _backGround.sprite = isCorrect ? _freeSprite : _errorSprite;

    public void UnHighlight() => _backGround.sprite = _emptySprite;
}