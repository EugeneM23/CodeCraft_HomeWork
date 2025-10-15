using Game.Scripts.GameObject.Player;

namespace Gameplay
{
    public class JumpController : IInitializeble, IDisposable
    {
        private InputReader _inputReader;
        private CollisionComponent _collisionComponent;
        private JumpComponent _jumpComponent;
            
        [Inject]
        private void Construct(
            InputReader inputReader,
            JumpComponent jumpComponent,
            CollisionComponent collisionComponent
        )
        {
            _collisionComponent = collisionComponent;
            _jumpComponent = jumpComponent;
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            _inputReader.OnJump += _jumpComponent.Jump;
            _collisionComponent.OnGrounded += _jumpComponent.ResetJump;
        }

        public void Dispose() => _inputReader.OnJump -= _jumpComponent.Jump;
    }
}