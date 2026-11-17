using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Hands the vehicle in the player's Character slot to the player: its Driver becomes the player
    /// context and it gets the Player tag, so its engine runs, its guns fire and enemies go for it.
    /// The vehicle that leaves the slot loses both. The player is always in the tank: the slot
    /// holds it from the start, and nothing gets in or out.
    /// </summary>
    public sealed class PlayerVehicleController : IPlayerContextInit, IPlayerContextDispose
    {
        private IPlayerContext _context;
        private Subscription<IEntity> _characterSubscription;
        private IEntity _vehicle;

        public void Init(IPlayerContext context)
        {
            _context = context;
            _characterSubscription = context.GetCharacter().Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            Release();
        }

        private void OnCharacterChanged(IEntity character)
        {
            Release();

            if (character == null || !character.TryGetDriver(out IReactiveVariable<IEntity> driver))
                return;

            _vehicle = character;
            driver.Value = _context;
            if (!character.HasPlayerTag())
                character.AddPlayerTag();
        }

        private void Release()
        {
            if (_vehicle == null)
                return;

            if (_vehicle.TryGetDriver(out IReactiveVariable<IEntity> driver) && driver.Value == _context)
                driver.Value = null;
            _vehicle.DelPlayerTag();
            _vehicle = null;
        }
    }
}
