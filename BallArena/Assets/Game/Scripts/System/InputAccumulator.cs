using Fusion;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Собирает ввод один раз на тик, а не на кадр. Нажатие даша накапливается между тиками,
    /// иначе тап, целиком уложившийся между двумя тиками, потеряется.
    /// Лежит на том же GameObject, что NetworkRunner и NetworkEvents.
    /// </summary>
    public sealed class InputAccumulator : MonoBehaviour
    {
        [SerializeField]
        private NetworkEvents _networkEvents;

        private bool _dashQueued;

        private void OnEnable()
        {
            _networkEvents.OnInput.AddListener(this.OnInput);
        }

        private void OnDisable()
        {
            _networkEvents.OnInput.RemoveListener(this.OnInput);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                _dashQueued = true;
        }

        private void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // GetAxisRaw, а не GetAxis: сглаживание Unity идёт по кадрам и дало бы
            // разные значения оси на пирах с разным FPS.
            var move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            bool dash = _dashQueued || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            var data = new InputData
            {
                Move = Vector2.ClampMagnitude(move, 1f),
            };
            data.Buttons.Set(InputButton.Dash, dash);

            input.Set(data);

            _dashQueued = false;
        }
    }
}
