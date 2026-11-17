using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Leaves footprints in mud. Each foot of a humanoid rig is watched (<see cref="FootPlant"/>);
    /// when one comes down while the character is moving, a flat-bottomed oval is pressed into the
    /// mud under it, pointing the way the character faces. A rig that is not humanoid leaves none.
    /// </summary>
    public sealed class CharacterFootprintsBehaviour : IEntityInit, IEntityTick
    {
        /// <summary>Slowest (m/s) a character still leaves prints at; slower is shuffling on the spot.</summary>
        private const float MIN_SPEED = 0.3f;

        /// <summary>A person's foot (m): half its length and half its width.</summary>
        private const float HALF_LENGTH = 0.13f;
        private const float HALF_WIDTH = 0.055f;

        /// <summary>How far (m) the middle of the sole is ahead of the ankle bone.</summary>
        private const float SOLE_AHEAD = 0.06f;

        /// <summary>Share of the mud one step presses away; steps on the same spot add up.</summary>
        private const float DEPTH = 0.5f;

        private Animator _animator;
        private Transform _root;
        private IReactiveVariable<float> _velocity;

        private Transform _leftFoot;
        private Transform _rightFoot;
        private FootPlant _left;
        private FootPlant _right;
        private float _scale = 1f;

        public void Init(IEntity entity)
        {
            _animator = entity.GetAnimator();
            _root = entity.GetTransform();
            _velocity = entity.GetVelocity();

            if (_animator == null || !_animator.isHuman)
                return;

            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _scale = _animator.humanScale > 0f ? _animator.humanScale : 1f;
            _left = new FootPlant(_scale);
            _right = new FootPlant(_scale);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_leftFoot == null || _rightFoot == null || deltaTime <= 0f)
                return;

            bool moving = _velocity.Value >= MIN_SPEED;
            Foot(_leftFoot, _left, moving, deltaTime);
            Foot(_rightFoot, _right, moving, deltaTime);
        }

        private void Foot(Transform foot, FootPlant plant, bool moving, float deltaTime)
        {
            float height = foot.position.y - _root.position.y;
            if (!plant.Step(height, deltaTime) || !moving)
                return;

            Vector3 forward = _root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            forward.Normalize();

            Vector3 sole = foot.position + forward * (SOLE_AHEAD * _scale);
            if (!MudImpactUseCase.TryFindBelow(sole, height + 0.3f, out MudSurface surface, out Vector3 ground))
                return;

            surface.Dent(ground, forward, HALF_LENGTH * _scale, HALF_WIDTH * _scale, DEPTH, 4f, 0.15f, 0.25f);
        }
    }
}
