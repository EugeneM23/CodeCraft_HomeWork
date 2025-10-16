namespace Gameplay.Ability
{
    public class PushAbilityController : IInitializeble, IDisposable
    {
        [Inject] private readonly InputReader _input;
        [Inject] private readonly Player _player;
        [Inject] private readonly PushAbility _ability;

        private IPushSideComponent _sideComponent;
        private IPushUpComponent _upComponent;

        public PushAbilityController(PushAbility ability)
        {
            _ability = ability;
        }

        public void Initialize()
        {
            _sideComponent = _player;
            _upComponent = _player;

            _input.OnFire += _sideComponent.Push;
            _input.OnFire += _upComponent.Push;
        }

        public void Dispose()
        {
            _input.OnFire -= _sideComponent.Push;
            _input.OnFire -= _upComponent.Push;
        }
    }
}