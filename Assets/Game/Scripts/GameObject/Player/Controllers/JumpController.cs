namespace Gameplay
{
    public class JumpController : IInitializeble
    {
        private InputReader _inputReader;
        private CollisionComponent _collisionComponent;
        private JumpComponent _jumpComponent;

        [Inject]
        private void Construct(InputReader inputReader, CollisionComponent collisionComponent,
            JumpComponent jumpComponent)
        {
            _inputReader = inputReader;
            _collisionComponent = collisionComponent;
            _jumpComponent = jumpComponent;
        }

        public void Initialize()
        {
            _inputReader.OnJump += _jumpComponent.Jump;
            _collisionComponent.OnGrounded += _jumpComponent.ResetJump;
        }

        private void OnDisable()
        {
            _inputReader.OnJump -= _jumpComponent.Jump;
            _collisionComponent.OnGrounded -= _jumpComponent.ResetJump;
        }
    }
}