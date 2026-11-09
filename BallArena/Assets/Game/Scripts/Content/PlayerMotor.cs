using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Единственный шлюз ввода на префабе игрока: один GetInput на объект.
    /// Капсула — динамический Rigidbody, а не кинематика: кинематический сдвиг
    /// transform не толкает мяч как надо, а нам нужен честный физический удар.
    ///
    /// Порядок «скорость выставлена до Physics.Simulate этого тика» задать нечем:
    /// [OrderBefore]/[OrderAfter] — это API Fusion 1, в 2.0.5 таких атрибутов нет.
    /// Цена — максимум один тик задержки, и он одинаков на всех пирах, поэтому
    /// детерминизм не страдает.
    /// </summary>
    public sealed class PlayerMotor : NetworkBehaviour
    {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private float _speed = 7f;

        [SerializeField]
        private float _acceleration = 45f;

        [SerializeField]
        private float _dashSpeed = 18f;

        [SerializeField]
        private float _dashCooldownSeconds = 1.5f;

        /// <summary>
        /// Обязан быть [Networked]: обычное поле не откатывается, и при ресимуляции
        /// даш сработал бы повторно.
        /// </summary>
        [Networked]
        private NetworkButtons _previousButtons { get; set; }

        [Networked]
        private TickTimer _dashCooldown { get; set; }

        public override void Spawned()
        {
            // Парная настройка к ClientPhysicsSimulation = SimulateAlways.
            // Без неё прокси форсится в isKinematic = true и дёргается.
            this.Runner.SetIsSimulated(this.Object, true);
        }

        public override void FixedUpdateNetwork()
        {
            // GetInput — это и есть authority-гейт: true только у владельца ввода и у хоста.
            if (!this.GetInput(out InputData input))
                return;

            var wish = new Vector3(input.Move.x, 0f, input.Move.y);

            Vector3 velocity = _rigidbody.linearVelocity;
            var planar = new Vector3(velocity.x, 0f, velocity.z);

            // Runner.DeltaTime, а не Time.deltaTime: на ресимуляции второй не равен шагу тика.
            planar = Vector3.MoveTowards(planar, wish * _speed, _acceleration * this.Runner.DeltaTime);

            bool dashPressed = input.Buttons.WasPressed(_previousButtons, InputButton.Dash);

            if (dashPressed && _dashCooldown.ExpiredOrNotRunning(this.Runner) && wish.sqrMagnitude > 0.01f)
            {
                _dashCooldown = TickTimer.CreateFromSeconds(this.Runner, _dashCooldownSeconds);
                planar = wish.normalized * _dashSpeed;
            }

            _rigidbody.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);

            _previousButtons = input.Buttons;
        }
    }
}
