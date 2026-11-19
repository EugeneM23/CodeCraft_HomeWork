using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Puts the combat systems into the level: the services in the GameContext at init, and the tank's part
    /// (loadout, skills, auto weapons, special module) on the player's tank whenever the player gets one.
    /// Added by <see cref="GameContextInstaller"/>. Each area owns its own setup:
    /// <see cref="TankModulesSetup"/> (loadout, stun, smoke), <see cref="ProgressionSetup"/> (experience,
    /// skills), <see cref="TargetingSetup"/> (auto guns, turrets, air strike, horde targets).
    /// </summary>
    public sealed class CombatBehaviour : IGameContextInit, IGameContextDispose
    {
        private GameContext _context;
        private CombatConfig _config;
        private Subscription<IEntity> _characterSubscription;
        private bool _subscribed;
        private IEntity _tank;

        public void Init(IGameContext context)
        {
            _context = (GameContext)context;
            _config = CombatConfig.Load();

            TargetingSetup.InstallContext(_context, _config);
            TankModulesSetup.InstallContext(_context, _config);
            ProgressionSetup.InstallContext(_context, _config);

            if (_context.TryGetPlayerContext(out PlayerContext player) && player != null)
            {
                _characterSubscription = player.GetCharacter().Observe(OnCharacterChanged);
                _subscribed = true;
            }
            else
            {
                Debug.LogWarning("[CombatBehaviour] No PlayerContext: combat stays without a tank.");
            }
        }

        public void Dispose(IGameContext context)
        {
            if (_subscribed)
                _characterSubscription.Dispose();
            _subscribed = false;

            Detach();

            ProgressionSetup.UninstallContext(_context);
            TankModulesSetup.UninstallContext(_context);
            TargetingSetup.UninstallContext(_context);
        }

        private void OnCharacterChanged(IEntity character)
        {
            if (ReferenceEquals(character, _tank))
                return;

            Detach();

            if (character == null || !character.HasVehicleTag())
                return;

            _tank = character;
            TankModulesSetup.AttachTank(_context, _tank, _config);
            ProgressionSetup.AttachTank(_context, _tank, _config);
            TargetingSetup.AttachTank(_context, _tank, _config);
        }

        /// <summary>
        /// The level's run came after the tank was attached (the core's RunBootstrap wakes after the context): the
        /// tank's run-bound parts (skill effects, defeat on death) are made again with it.
        /// </summary>
        public void RebindRun()
        {
            if (_tank == null)
                return;
            ProgressionSetup.DetachTank(_context, _tank);
            ProgressionSetup.AttachTank(_context, _tank, _config);
        }

        private void Detach()
        {
            if (_tank == null)
                return;

            TargetingSetup.DetachTank(_context, _tank);
            ProgressionSetup.DetachTank(_context, _tank);
            TankModulesSetup.DetachTank(_context, _tank);
            _tank = null;
        }
    }
}
