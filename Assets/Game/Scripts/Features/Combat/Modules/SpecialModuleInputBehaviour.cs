using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// E on the player's tank: the special module (CombatAPI.SpecialModule) goes off when the player presses it
    /// while driving the intact tank. Reads the player's IPlayerInput off the PlayerContext. Attached by
    /// <see cref="TankModulesSetup"/>.
    /// </summary>
    public sealed class SpecialModuleInputBehaviour : IEntityInit, IEntityTick
    {
        private readonly PlayerContext _player;
        private IPlayerInput _input;
        private IValue<IEntity> _driver;
        private Health _health;

        public SpecialModuleInputBehaviour(PlayerContext player)
        {
            _player = player;
        }

        public void Init(IEntity entity)
        {
            _driver = entity.TryGetDriver(out IReactiveVariable<IEntity> driver) ? driver : null;
            entity.TryGetHealth(out _health);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // The input may come up after the tank: looked up until found (a key lookup, no allocation).
            if (_input == null && (_player == null || !_player.TryGetPlayerInput(out _input)))
                return;

            if (!_input.SpecialPressed)
                return;
            if (_driver != null && _driver.Value == null)
                return;
            if (_health != null && !_health.Exists())
                return;

            if (entity.TryGetSpecialModule(out ISpecialModule module) && module != null)
                module.TryActivate();
        }
    }
}
