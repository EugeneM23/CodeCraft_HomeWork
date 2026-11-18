using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Raises <see cref="CombatSignals.CannonFired"/> (muzzle, game time) on every shot of the tank's cannon, off the
    /// cannon weapon's FireEvent. The elite hears it. On the tank; added by <see cref="TargetingSetup"/>.
    /// </summary>
    public sealed class CannonFiredSignalBehaviour : IEntityInit, IEntityDispose
    {
        private IEvent _fired;
        private Transform _muzzle;
        private Transform _fallback;
        private Action _onFired;

        public void Init(IEntity tank)
        {
            tank.TryGetTransform(out _fallback);
            if (!tank.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                return;

            IEntity cannon = weapon.Value;
            cannon.TryGetFirePoint(out _muzzle);
            if (!cannon.TryGetFireEvent(out _fired) || _fired == null)
                return;

            _onFired = OnFired;
            _fired.OnEvent += _onFired;
        }

        public void Dispose(IEntity tank)
        {
            if (_fired != null && _onFired != null)
                _fired.OnEvent -= _onFired;
            _fired = null;
        }

        private void OnFired()
        {
            Transform from = _muzzle != null ? _muzzle : _fallback;
            CombatSignals.RaiseCannonFired(from != null ? from.position : Vector3.zero, GameTicks.Time);
        }
    }
}
