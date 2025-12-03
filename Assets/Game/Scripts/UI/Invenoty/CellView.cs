using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptyCell;
    [SerializeField] private Sprite _occupiedCell;
    [SerializeField] private Sprite _arroredCell;

    private InventoryPresenter _presenter;

    public Vector2Int ItemMatrixPosition;
    public Vector2Int GridPosition;
    public Item Item { get; private set; }
    public InventoryItem InventoryItem { get; private set; }
    public InventoryPresenter Presenter => _presenter;

    public void SetItem(Item item, InventoryItem inventoryItem)
    {
        Item = item;
        InventoryItem = inventoryItem;
    }

    public void Clear()
    {
        Item = null;
        InventoryItem = null;
    }

    public void Highlight(bool isCorrect)
    {
        _backGround.sprite = isCorrect ? _occupiedCell : _arroredCell;
    }

    public void UnHighlight()
    {
        _backGround.sprite = _emptyCell;
    }

    public void SetPresenter(InventoryPresenter presenter) => _presenter = presenter;
}