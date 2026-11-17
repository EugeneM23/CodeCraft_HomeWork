using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A smoke spot on the map: sets a <see cref="VoxelSmoke"/> cloud off where it stands and, once
    /// that one has thinned away, another after a short pause — the map always has smoke to shoot
    /// through.
    /// </summary>
    public sealed class VoxelSmokeSource : MonoBehaviour, IVoxelUpdate
    {
        [SerializeField] private float _radius = 3.2f;
        [SerializeField] private float _lifetime = 20f;
        [Tooltip("Seconds of clear air between one cloud and the next.")]
        [SerializeField] private float _pause = 3f;
        [Tooltip("Seconds before the first cloud: spots set off together would all thin away together.")]
        [SerializeField] private float _delay;

        private VoxelSmoke _cloud;
        private float _wait;
        private bool _started;

        public VoxelSmoke Cloud => _cloud;

        public void Set(float radius, float lifetime, float pause, float delay)
        {
            _radius = radius;
            _lifetime = lifetime;
            _pause = pause;
            _delay = delay;
        }

        private void Start()
        {
            _wait = _delay;
            _started = true;
        }

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void Update() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelUpdate(Time.deltaTime);
        }

        // Driven, the hub can reach a source added mid-frame before its Start: the delay is not set yet
        public void VoxelUpdate(float deltaTime)
        {
            if (_cloud != null || !_started)
                return;

            _wait -= deltaTime;
            if (_wait > 0f)
                return;

            _cloud = VoxelSmoke.Deploy(transform.position, _radius, _lifetime);
            _cloud.transform.SetParent(transform, true);
            _wait = _pause;
        }
    }
}
