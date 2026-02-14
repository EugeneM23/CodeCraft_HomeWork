using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _itemImage;
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private GameObject _countBackGround;
        [SerializeField] private TMP_Text _count;
        private InventoryUI _ui;
        
        [Inject] private SignalBus _signalBus;


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

        public void OnPointerClick(PointerEventData eventData)
        {
            _signalBus.Fire(new ItemRemovedSignal(this));
            Debug.Log("Clicked");
        }

        public void SetView(InventoryUI inventoryUI)
        {
             _ui = inventoryUI;
        }
    }
}