using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inventories
{
    public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _itemImage;
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private GameObject _countBackGround;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private DoubleClickHandler _doubleClick;

        private ItemUseCase _itemUseCase;
        public Item Item { get; private set; }

        private InventoryPresenter _presenter;

        private void OnEnable()
        {
            _doubleClick.OnDoubleClick += OnDoubleClicked;
            _itemImage.rectTransform.localScale = Vector3.one;
            SetBackgroundAlpha(0f);
        }

        private void OnDisable()
        {
            _doubleClick.OnDoubleClick -= OnDoubleClicked;
        }

        private void OnDoubleClicked()
        {
            _itemUseCase.Invoke(_presenter, Item);
        }

        private void OnDestroy()
        {
            if (Item != null)
                Item.OnStackChanged -= UpdateQuantity;
        }

        private void UpdateQuantity(int quantity)
        {
            _count.text = quantity.ToString();
        }

        public void SetupItem(Item item, Vector2 cellSize, InventoryPresenter presenter)
        {
            if (Item != null)
                Item.OnStackChanged -= UpdateQuantity;

            Item = item;
            _itemImage.sprite = item.itemData.Icon;

            bool showCount = item.CanStack;
            _count.gameObject.SetActive(showCount);
            _countBackGround.SetActive(showCount);

            if (showCount)
                _count.text = item.StackQuantity.ToString();

            Item.OnStackChanged += UpdateQuantity;

            Vector2 itemSize = new Vector2(
                cellSize.x * item.itemData.Size.x,
                cellSize.y * item.itemData.Size.y
            );

            _rectTransform.sizeDelta = itemSize;
            _presenter = presenter;
            _itemUseCase = item.ItemUseCase;
        }

        public void SetIcon(Sprite icon)
        {
            _itemImage.sprite = icon;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _itemImage.transform.localScale *= 0.8f;

            SetBackgroundAlpha(1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _itemImage.transform.localScale = Vector3.one;
            SetBackgroundAlpha(0f);
        }

        private void SetBackgroundAlpha(float alpha)
        {
            Color color = _background.color;
            color.a = alpha;
            _background.color = color;
        }
    }
}