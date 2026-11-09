using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Табличка с ником. Никакой сетевой осведомлённости: текст ей подаёт PlayerIdentity.
    /// </summary>
    public sealed class Nameplate : MonoBehaviour
    {
        [SerializeField]
        private TextMesh _text;

        public void SetText(string value)
        {
            if (_text != null)
                _text.text = value;
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera != null)
                this.transform.rotation = camera.transform.rotation;
        }
    }
}
