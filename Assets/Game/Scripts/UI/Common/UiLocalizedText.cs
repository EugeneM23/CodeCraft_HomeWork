using System;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A static label: its TMP_Text shows UiStrings.Get(key) when enabled and again when the language changes.
    /// Not for texts a screen writes at run time (they would be overwritten here).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UiLocalizedText : MonoBehaviour
    {
        [Tooltip("Key in UiStringTable, e.g. main.play")]
        [SerializeField] private string _key;

        private TMP_Text _text;
        private Action _onLanguageChanged;

        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                if (isActiveAndEnabled)
                    Refresh();
            }
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _onLanguageChanged = Refresh;
        }

        private void OnEnable()
        {
            UiStrings.LanguageChanged += _onLanguageChanged;
            Refresh();
        }

        private void OnDisable() => UiStrings.LanguageChanged -= _onLanguageChanged;

        private void Refresh()
        {
            if (_text != null && !string.IsNullOrEmpty(_key))
                _text.text = UiStrings.Get(_key);
        }
    }
}
