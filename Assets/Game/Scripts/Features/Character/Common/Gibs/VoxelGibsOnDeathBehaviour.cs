using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// On death the body becomes voxel flesh (VoxelGibs), thrown from the killing hit: the pose is
    /// read at once, the flesh takes the body's place two frames on. The killer is remembered from
    /// the last damage event (see GibRules.ReadHit).
    /// </summary>
    public sealed class VoxelGibsOnDeathBehaviour : IEntityInit, IEntityDispose
    {
        private readonly VoxelGibsRig _rig;
        private readonly VoxelGibsConfig _config;

        private IEvent<TakeDamageArgs> _damageTakenEvent;
        private IEvent<TakeDamageArgs> _deathTakenEvent;
        private TakeDamageArgs _lastHit;

        // Made once: a method group passed to Unsubscribe would be a new delegate (garbage) on the death frame.
        private System.Action<TakeDamageArgs> _onDamageTaken;
        private System.Action<TakeDamageArgs> _onDeath;

        public VoxelGibsOnDeathBehaviour(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            _rig = rig;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _onDamageTaken ??= this.OnDamageTaken;
            _onDeath ??= this.OnDeath;
            _damageTakenEvent.Subscribe(_onDamageTaken);
            _deathTakenEvent.Subscribe(_onDeath);

            // The first body of a level to come in (at level load) has the level's corpse bodies made, and the first of
            // the session plays a death through once, so the first death of a fight compiles nothing (VoxelGibs.Prepare).
            VoxelGibs.Prepare(_rig, _config);
        }

        // Null when disposed without having been initialised.
        public void Dispose(IEntity entity)
        {
            if (_onDamageTaken != null)
                _damageTakenEvent?.Unsubscribe(_onDamageTaken);
            if (_onDeath != null)
                _deathTakenEvent?.Unsubscribe(_onDeath);
        }

        private void OnDamageTaken(TakeDamageArgs args) => _lastHit = args;

        private void OnDeath(TakeDamageArgs _) =>
            VoxelGibs.Burst(_rig, _config, GibRules.ReadHit(_lastHit, _rig.Skeleton.position));
    }
}
