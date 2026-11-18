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

            // The first body of the session to come in (at level load) plays a death through once, so the first
            // death of a fight does not compile its code mid-frame (VoxelGibs.WarmUp).
            if (!s_warmedUp && UnityEngine.Application.isPlaying)
            {
                s_warmedUp = true;
                VoxelGibs.WarmUp(_rig, _config);
            }
        }

        private static bool s_warmedUp;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_warmedUp = false;

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
