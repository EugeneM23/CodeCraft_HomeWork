using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Takes a loose body (VoxelBodySettings.Settle) out of the simulation once it has lain still
    /// for a second (VoxelBody.Freeze: its Rigidbody turns kinematic): a creature's flesh then lies
    /// all game for the price of its meshes, not of physics. A settled body is not loose, so it is
    /// not frozen again; a blast lets it go (VoxelBody.CarveSphere), and this starts over.
    /// </summary>
    [RequireComponent(typeof(VoxelBody))]
    public sealed class VoxelSettle : MonoBehaviour, IVoxelFixedUpdate
    {
        private const float STILL_TIME = 1f;
        private const float STILL_SPEED = 0.05f;
        private const float STILL_SPIN = 0.1f;

        private VoxelBody _body;
        private Rigidbody _rigidbody;
        private float _still;

        private void Awake() => _body = GetComponent<VoxelBody>();

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void FixedUpdate() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelFixedUpdate(Time.fixedDeltaTime);
        }

        public void VoxelFixedUpdate(float deltaTime)
        {
            if (_body == null || !_body.IsLoose)
            {
                _still = 0f;
                _rigidbody = null;
                return;
            }

            if (_rigidbody == null && !TryGetComponent(out _rigidbody))
                return;
            if (_rigidbody.isKinematic)
            {
                _still = 0f;
                return;
            }

            bool still = _rigidbody.IsSleeping() ||
                         (_rigidbody.linearVelocity.sqrMagnitude < STILL_SPEED * STILL_SPEED &&
                          _rigidbody.angularVelocity.sqrMagnitude < STILL_SPIN * STILL_SPIN);
            _still = still ? _still + Time.fixedDeltaTime : 0f;
            if (_still < STILL_TIME)
                return;

            _still = 0f;
            _rigidbody = null;
            _body.Freeze();
        }
    }
}
