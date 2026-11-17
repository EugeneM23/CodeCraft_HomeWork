using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Takes the dead character out of the player context.
    /// </summary>
    /// <remarks>
    /// DeathBehaviour disposes the corpse, which clears its values and empties the expressions it
    /// held: the fire condition is an AndExpression, and an empty AND is true. The input
    /// controllers keep what they read from the character until Character changes, so holding
    /// Space over a dead player kept firing into a cleared entity (KeyNotFoundException: Weapon)
    /// every frame. They all already treat a null character as "no character".
    /// </remarks>
    public sealed class PlayerCharacterDeathController : IPlayerContextInit, IPlayerContextDispose
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;
        private Health _health;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            ReleaseHealth();
        }

        private void OnCharacterChanged(IEntity character)
        {
            ReleaseHealth();

            // A vehicle's death is its own business: TankDestroyBehaviour throws the driver out and
            // hands Character back to them. Clearing it here would race that hand-over, and if it
            // ran second the player would be left with no character at all.
            if (character != null && !character.HasVehicleTag() && character.TryGetHealth(out _health))
                _health.OnHealthEmpty += OnCharacterDied;
        }

        private void OnCharacterDied() => _characterSource.Value = null;

        private void ReleaseHealth()
        {
            if (_health != null)
                _health.OnHealthEmpty -= OnCharacterDied;

            _health = null;
        }
    }
}
