using UnityEngine;

namespace Gameplay
{
    public class FollowComponent : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _folowObject;
        [SerializeField] private float _smoothTime;

        private Vector3 _offset;

        private Vector3 _velocity = Vector3.zero;

        private void Start()
        {
            _offset = gameObject.transform.position - _target.position;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_folowObject != null && target != null)
                _offset = _folowObject.position - _target.transform.position;
        }

        private void LateUpdate()
        {
            if (_target == null || _folowObject == null) return;

            Vector3 targetPosition = _target.transform.position + _offset;
            _folowObject.position =
                Vector3.SmoothDamp(_folowObject.position, targetPosition, ref _velocity, _smoothTime);
        }
    }
}