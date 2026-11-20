using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game
{
    /// <summary>
    /// A body the tank drives into is knocked down first (the user): it plays its fall (a clip over its Animator, so
    /// the controller needs no state for it), stops walking, and the tank no longer bumps into it (its colliders
    /// exclude the tank's layers; shots still hit it). TankCrushBehaviour pops it once the hull is over it
    /// (<see cref="Down"/>). Left lying past <see cref="KnockdownInstaller"/>'s lie time it gets up and goes on.
    /// </summary>
    public sealed class KnockdownBehaviour : IEntityInit, IEntityTick, IEntityLateTick, IEntityDispose
    {
        /// <summary>Bodies lying knocked down now: the tank looks for the ones under its hull.</summary>
        public static readonly List<KnockdownBehaviour> Down = new(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Down.Clear();

        private readonly AnimationClip _fall;
        private readonly float _lie;

        private IEntity _entity;
        private Animator _animator;
        private Transform _transform;
        private Collider[] _colliders;
        private IReadOnlyList<Collider> _vehicle;
        private Rigidbody _body;
        private bool _wasKinematic;
        private PlayableGraph _graph;
        private float _lying;
        /// <summary>Where it went down: it stays there (its walking motor would push it out of the tank's hull).</summary>
        private Vector3 _lyingAt;

        public KnockdownBehaviour(AnimationClip fall, float lie)
        {
            _fall = fall;
            _lie = lie;
        }

        /// <summary>On the ground after a knockdown, not up yet.</summary>
        public bool IsDown { get; private set; }

        /// <summary>The body this knockdown belongs to.</summary>
        public IEntity Entity => _entity;

        /// <summary>Where it lies (its feet).</summary>
        public Vector3 Position => _transform != null ? _transform.position : Vector3.zero;

        public void Init(IEntity entity)
        {
            _entity = entity;
            _animator = entity.GetAnimator();
            _transform = entity.GetTransform();
            GameObject root = entity.GetGameObject();
            _colliders = root != null ? root.GetComponentsInChildren<Collider>(true) : new Collider[0];
            _body = root != null ? root.GetComponentInChildren<Rigidbody>() : null;
            // A body on the ground does not walk.
            if (entity.TryGetMoveCondition(out IExpression<bool> move))
                move.Add(new ExpressionMember<bool>(() => !IsDown));
        }

        /// <summary>
        /// Knocks it down: the fall plays from its start, it stops, and colliders on <paramref name="vehicleLayers"/> (the
        /// tank's colliders) pass over it. False when it is down already or has no fall.
        /// </summary>
        public bool Knock(IReadOnlyList<Collider> vehicle)
        {
            if (IsDown || _fall == null || _animator == null)
                return false;

            IsDown = true;
            _lying = 0f;
            _lyingAt = _transform.position;
            if (_entity.TryGetMoveDirection(out IReactiveVariable<Vector3> direction))
                direction.Value = Vector3.zero;
            // The tank rolls over it: no contacts with any of its colliders, and nothing shoves the body along.
            _vehicle = vehicle;
            Ignore(true);
            if (_body != null)
            {
                _wasKinematic = _body.isKinematic;
                _body.isKinematic = true;
            }

            if (_graph.IsValid())
                _graph.Destroy();
            AnimationClipPlayable clip = AnimationPlayableUtilities.PlayClip(_animator, _fall, out _graph);
            clip.SetDuration(_fall.length);
            Down.Add(this);
            return true;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (!IsDown)
                return;
            if (entity.TryGetHealth(out Health health) && health.IsEmpty())
            {
                // Dead where it lies (popped, shot): it stays down, its death takes the pose from here.
                Down.Remove(this);
                return;
            }

            Hold();
            _lying += deltaTime;
            if (_lying >= _lie)
                GetUp();
        }

        public void LateTick(IEntity entity, float deltaTime)
        {
            if (IsDown)
                Hold();
        }

        /// <summary>Keeps the body where it went down: the tank rolls onto it, nothing pushes it out of the hull's way.</summary>
        private void Hold()
        {
            if (_transform != null && _transform.position != _lyingAt)
                _transform.position = _lyingAt;
        }

        /// <summary>Back on its feet: its Animator plays again, the tank bumps into it again, it walks on.</summary>
        private void GetUp()
        {
            IsDown = false;
            Down.Remove(this);
            if (_graph.IsValid())
                _graph.Destroy();
            Ignore(false);
            if (_body != null)
                _body.isKinematic = _wasKinematic;
        }

        private void Ignore(bool ignore)
        {
            if (_vehicle == null)
                return;
            for (int i = 0; i < _colliders.Length; i++)
                for (int k = 0; k < _vehicle.Count; k++)
                    if (_colliders[i] != null && _vehicle[k] != null)
                        Physics.IgnoreCollision(_colliders[i], _vehicle[k], ignore);
        }

        public void Dispose(IEntity entity)
        {
            Down.Remove(this);
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
