using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inventories
{
    public class InventoryItemView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _itemImage;
        [SerializeField] private GameObject _countBackGround;
        [SerializeField] private TMP_Text _count;

        public void SetIcon(Sprite icon)
        {
            _itemImage.sprite = icon;
        }

        public void SetCount(int count)
        {
            if (count <= 1)
            {
                _countBackGround.SetActive(false);
            }
            else
            {
                _countBackGround.SetActive(true);
                _count.text = count.ToString();
            }
        }

        public void SetBackground(Sprite background)
        {
            _background.sprite = background;
        }

        public void SetBackgroundColor(Color color)
        {
            _background.color = color;
        }

        public Sprite GetIcon()
        {
            return _itemImage.sprite;
        }

    }
}