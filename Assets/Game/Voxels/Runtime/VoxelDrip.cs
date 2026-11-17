using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A piece just torn off bleeds as it flies: for a short while it drops blood from its torn
    /// faces, so a trail of drops marks its arc on the ground.
    /// </summary>
    public sealed class VoxelDrip : MonoBehaviour
    {
        private Vector3[] _points;
        private Color32[] _colors;
        private float _size;
        private float _until;
        private float _interval;
        private float _next;
        private Rigidbody _body;

        /// <param name="worldPoints">Where the drops come from, now; they ride with this object from here on.</param>
        public void Begin(IReadOnlyList<Vector3> worldPoints, Color32[] colors, float size, float duration, float rate)
        {
            if (worldPoints == null || worldPoints.Count == 0 || colors == null || colors.Length == 0)
            {
                Destroy(this);
                return;
            }

            _points = new Vector3[worldPoints.Count];
            for (int i = 0; i < _points.Length; i++)
                _points[i] = transform.InverseTransformPoint(worldPoints[i]);
            _colors = colors;
            _size = size;
            _until = Time.time + duration;
            _interval = 1f / Mathf.Max(0.01f, rate);
            _next = Time.time;
        }

        private void Update()
        {
            if (_points == null || Time.time >= _until)
            {
                Destroy(this);
                return;
            }

            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null)
                return;
            if (_body == null)
                TryGetComponent(out _body);

            while (_next <= Time.time)
            {
                _next += _interval;
                Vector3 point = transform.TransformPoint(_points[Random.Range(0, _points.Length)]);
                Vector3 velocity = _body != null && !_body.isKinematic ? _body.GetPointVelocity(point) : Vector3.zero;
                debris.Drop(point, velocity + Random.insideUnitSphere * 0.4f, _colors[Random.Range(0, _colors.Length)], _size);
            }
        }
    }
}
