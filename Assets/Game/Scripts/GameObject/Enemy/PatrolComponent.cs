using UnityEngine;

namespace Gameplay
{
    public class PatrolComponent : IInitializeble, ITickable
    {
        [Inject] private Transform _character;
        [Inject] private EnemyMoveController _move;

        private readonly Transform[] _points;
        private int _index;
        private const float ReachThreshold = 0.2f;

        public bool IsActive { get; set; } = true;

        public PatrolComponent(Transform[] points) => _points = points;

        public void Initialize() => _index = 0;

        public void Tick()
        {
            if (!IsActive || _points.Length == 0) return;

            var target = _points[_index];
            var dir = target.position - _character.position;
            dir.y = 0;

            if (dir.magnitude < ReachThreshold)
                _index = (_index + 1) % _points.Length;
            else
                _move.Move(dir.normalized);
        }
    }
}