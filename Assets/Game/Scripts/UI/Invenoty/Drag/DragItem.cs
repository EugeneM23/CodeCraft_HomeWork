using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragItem : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    public Item Item { get; set; }
    public Sprite Icon => _itemIcon.sprite;
    public IInventoryCollection Presenter { get; set; }
    public Vector2Int MatrixPosition { get; set; }
    public Vector2Int StartPosition { get; set; }

    public void SetIcon(Sprite icon)
    {
        _itemIcon.sprite = icon;
    }
}