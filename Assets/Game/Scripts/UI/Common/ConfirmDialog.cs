using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// "Are you sure?" over the current screen: a title, a text, Yes and No. Asked through
    /// ScreenFlow.Ask, which pushes it and hands the answer back after it has left the stack.
    /// </summary>
    public sealed class ConfirmDialog : UIScreen
    {
        public event Action<bool> Chosen;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _yes;
        [SerializeField] private Button _no;
        [SerializeField] private TMP_Text _yesLabel;
        [SerializeField] private TMP_Text _noLabel;

        public void Setup(string title, string text, string yes, string no)
        {
            _title.text = title;
            _text.text = text;
            _yesLabel.text = yes;
            _noLabel.text = no;
        }

        private void OnEnable()
        {
            _yes.onClick.AddListener(HandleYes);
            _no.onClick.AddListener(HandleNo);
        }

        private void OnDisable()
        {
            _yes.onClick.RemoveListener(HandleYes);
            _no.onClick.RemoveListener(HandleNo);
        }

        private void HandleYes() => Chosen?.Invoke(true);
        private void HandleNo() => Chosen?.Invoke(false);
    }
}
