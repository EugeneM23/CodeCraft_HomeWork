using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fire and smoke at the cannon's muzzle on every shot: a pooled burst — flash, flame, a blast
    /// of smoke along the barrel, a cloud round the muzzle — and then a thin wisp still leaking
    /// out of the barrel for a few seconds.
    ///
    /// The burst simulates in world space, so it stays where the shot was fired while the tank
    /// drives on. The wisp lives on the barrel and follows it; it is restarted on every shot, its
    /// emission thinning to nothing over its own duration.
    /// </summary>
    public sealed class TankMuzzleSmokeBehaviour : IEntityInit, IEntityDispose
    {
        private readonly GameContext _gameContext;
        private readonly MonoEntity _burstPrefab;
        private readonly ParticleSystem _wisp;

        private Transform _firePoint;
        private IEvent _fireEvent;

        public TankMuzzleSmokeBehaviour(GameContext gameContext, MonoEntity burstPrefab, ParticleSystem wisp)
        {
            _gameContext = gameContext;
            _burstPrefab = burstPrefab;
            _wisp = wisp;
        }

        public void Init(IEntity entity)
        {
            if (!entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                return;

            weapon.Value.TryGetFirePoint(out _firePoint);

            if (weapon.Value.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;
        }

        private void OnFire()
        {
            if (_firePoint != null && _burstPrefab != null)
            {
                IEntity burst = _gameContext.GetGameFactory().Create(_burstPrefab);
                burst.GetTransform().SetPositionAndRotation(_firePoint.position, _firePoint.rotation);
            }

            if (_wisp != null)
            {
                // Stopping only the emission keeps the wisp already in the air; Play then starts
                // the thinning-out again from the top.
                _wisp.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _wisp.Play(true);
            }
        }
    }
}
