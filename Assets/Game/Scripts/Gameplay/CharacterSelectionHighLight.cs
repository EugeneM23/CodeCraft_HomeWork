using HighlightPlus;
using UnityEngine;

namespace Game.Scripts
{
    public class CharacterSelectionHighLight : MonoBehaviour
    {
        [SerializeField] private HighlightEffect _highlightEffect;
        [SerializeField] private Entity _entity;

        [SerializeField] private CharacterSelector _selector;

        private void Start()
        {
            _highlightEffect.highlighted = false;
        }

        private void OnEnable()
        {
            _selector.OnUnitChanged += HandleHighlight;
        }

        private void HandleHighlight(Entity character)
        {
            _highlightEffect.highlighted = character == _entity;
        }
    }
}