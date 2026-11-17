using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Keeps the camera from climbing with the helicopter. The follow camera sits at a fixed
    /// offset from CameraPoint; were the point on the helicopter, the camera would rise the whole
    /// cruise height with it.
    ///
    /// The point is put on the line from the camera through the helicopter, slid down it to
    /// <see cref="_heightShare"/> of the helicopter's height above the ground. On that line the
    /// helicopter stays where the point is on screen — in the middle — while the camera stays low;
    /// the helicopter just looks nearer.
    /// </summary>
    public sealed class HelicopterCameraPointBehaviour : IEntityInit, IEntityTick
    {
        private readonly Transform _point;
        private readonly float _ground;
        private readonly float _heightShare;

        private Transform _body;
        private Camera _camera;

        // The last point written and where the point stood right after. With both the same again
        // the write is skipped: a parked helicopter's point stays put, and writing it every frame
        // only marked it changed.
        private Vector3 _placed;
        private Vector3 _held;
        private bool _written;

        /// <param name="heightShare">0: the camera stays at its on-foot height; 1: it climbs all the way with the helicopter.</param>
        public HelicopterCameraPointBehaviour(Transform point, float groundHeight, float heightShare)
        {
            _point = point;
            _ground = groundHeight;
            _heightShare = Mathf.Clamp01(heightShare);
        }

        public void Init(IEntity entity) => _body = entity.GetTransform();

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_point == null)
                return;

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                    return;
            }

            Vector3 placed = Place(_body.position, _camera.transform.forward, _ground, _heightShare);
            if (_written && placed.Equals(_placed) && _point.position.Equals(_held))
                return;

            _point.position = placed;
            _placed = placed;
            _held = _point.position;
            _written = true;
        }

        /// <summary>
        /// Where the camera point goes for a body at <paramref name="body"/>, seen along
        /// <paramref name="view"/>. A view that does not look down leaves the point on the body.
        /// </summary>
        public static Vector3 Place(Vector3 body, Vector3 view, float ground, float heightShare)
        {
            float drop = (body.y - ground) * (1f - Mathf.Clamp01(heightShare));

            if (drop <= 0f || view.y > -1e-3f)
                return body;

            return body + view * (drop / -view.y);
        }
    }
}
