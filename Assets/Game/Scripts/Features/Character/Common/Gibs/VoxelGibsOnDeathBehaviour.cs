using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// On death the body becomes voxel flesh (VoxelGibs), thrown from the killing hit. The killer
    /// is remembered from the last damage event (see GibRules.ReadHit).
    /// </summary>
    public sealed class VoxelGibsOnDeathBehaviour : IEntityInit, IEntityDispose
    {
        private readonly VoxelGibsRig _rig;
        private readonly VoxelGibsConfig _config;

        private IEvent<TakeDamageArgs> _damageTakenEvent;
        private IEvent<TakeDamageArgs> _deathTakenEvent;
        private TakeDamageArgs _lastHit;

        public VoxelGibsOnDeathBehaviour(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            _rig = rig;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _damageTakenEvent.Subscribe(this.OnDamageTaken);
            _deathTakenEvent.Subscribe(this.OnDeath);

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
            _damageTakenEvent?.Unsubscribe(this.OnDamageTaken);
            _deathTakenEvent?.Unsubscribe(this.OnDeath);
        }

        private void OnDamageTaken(TakeDamageArgs args) => _lastHit = args;

        private void OnDeath(TakeDamageArgs _) =>
            VoxelGibs.Burst(_rig, _config, GibRules.ReadHit(_lastHit, _rig.Skeleton.position));
    }
}
