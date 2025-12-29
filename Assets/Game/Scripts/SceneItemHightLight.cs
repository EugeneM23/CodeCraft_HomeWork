using System;
using HighlightPlus;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scripts
{
    public class SceneItemHighLight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private HighlightEffect highlightEffect;

        private void Start()
        {
            highlightEffect.highlighted = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            highlightEffect.highlighted = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            highlightEffect.highlighted = false;
        }
    }
}