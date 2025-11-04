using System;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class TargetSensor : IInitializeble, IDisposable
    {
        public event Action<RaycastHit2D> OnHitTarget;

        [Inject] private readonly SpriteAnimator _spriteAnimator;
        [Inject] private readonly CharacterController2D _character;

        private readonly LayerMask _layerMask;

        private readonly float _castRadius = 0.5f;
        private readonly float _castDistance = 2f;

        public TargetSensor(LayerMask layerMask)
        {
            _layerMask = layerMask;
        }

        public void Initialize()
        {
            _spriteAnimator.OnEventRaised += Sens;
            Physics2D.queriesStartInColliders = false;
        }

        private void Sens(EventID id)
        {
            if (id != EventID.CastDamage)
                return;

            Vector2 castDirection = new Vector2(_character.LookDirection, 0);
            Vector2 castOrigin = _character.Collider.bounds.center;

            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                castOrigin,
                _castRadius,
                castDirection,
                _castDistance,
                _layerMask
            );

            if (hits.Length > 0)
                OnHitTarget?.Invoke(hits[0]);

            Debug.DrawRay(castOrigin, castDirection * _castDistance, Color.red, 0.5f);
        }

        public void Dispose()
        {
            _spriteAnimator.OnEventRaised -= Sens;
        }
    }
}