using Game.Scripts.GameObject.Player;

namespace Gameplay
{
    public class JumpController : IInitializeble, IDisposable
    {
        private InputReader _inputReader;
        private CollisionComponent _collisionComponent;
        private ImpulseComponent _impulseComponent;
            
        [Inject]
        private void Construct(
            InputReader inputReader,
            ImpulseComponent impulseComponent,
            CollisionComponent collisionComponent
        )
        {
            _collisionComponent = collisionComponent;
            _impulseComponent = impulseComponent;
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            _inputReader.OnJump += _impulseComponent.AddForce;
        }

        public void Dispose() => _inputReader.OnJump -= _impulseComponent.AddForce;
    }
}