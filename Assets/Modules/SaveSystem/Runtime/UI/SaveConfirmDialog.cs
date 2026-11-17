using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Saving.UI
{
    /// <summary>A yes/no question over the slot menu: "Overwrite Slot 2?". No is selected first.</summary>
    public sealed class SaveConfirmDialog : MonoBehaviour
    {
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _yes;
        [SerializeField] private Button _no;

        private Action _onYes;
        private bool _wired;

        public bool IsOpen => gameObject.activeSelf;
        public string Message => _message != null ? _message.text : string.Empty;

        public void Ask(string message, Action onYes)
        {
            Wire();
            _onYes = onYes;
            if (_message != null)
                _message.text = message;

            gameObject.SetActive(true);
            if (_no != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_no.gameObject);
        }

        public void Confirm()
        {
            Action onYes = _onYes;
            Hide();
            onYes?.Invoke();
        }

        public void Cancel() => Hide();

        public void Hide()
        {
            _onYes = null;
            gameObject.SetActive(false);
        }

        private void Wire()
        {
            if (_wired)
                return;

            _wired = true;
            if (_yes != null)
                _yes.onClick.AddListener(Confirm);

            if (_no != null)
                _no.onClick.AddListener(Cancel);
        }
    }
}
