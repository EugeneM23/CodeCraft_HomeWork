using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;

    public Vector2Int GridPosition { get; private set; }

    public InventoryItem InventoryItem { get; set; }

    public InventoryPresenter Presenter { get; private set; }

    public void Construct(InventoryPresenter presenter, Vector2Int gridPosition, CellView[,] cells)
    {
        Presenter = presenter;
        GridPosition = gridPosition;
    }

    public void Highlight(bool isCorrect) => _backGround.sprite = isCorrect ? _freeSprite : _errorSprite;

    public void UnHighlight() => _backGround.sprite = _emptySprite;

    public void Clear()
    {
        Presenter = null;
        InventoryItem = null;
    }
}