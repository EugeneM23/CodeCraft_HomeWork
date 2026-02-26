using Inventories;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    [SerializeField] private GameObject _countBackGround;
    [SerializeField] private TMP_Text _count;

    public void Setup(ItemSettings itemSettings)
    {
        _itemImage.sprite = itemSettings.Icon;
        SetBackgroundAlpha(0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetBackgroundAlpha(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetBackgroundAlpha(0f);
    }

    private void SetBackgroundAlpha(float alpha)
    {
        Color color = _background.color;
        color.a = alpha;
        _background.color = color;
    }
}