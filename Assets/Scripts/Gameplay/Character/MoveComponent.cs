using System;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class MoveComponent
    {
        [SerializeField] private Transform _transform;
        [SerializeField] private float _speed = 5;

        public void Move(Vector3 direction)
        {
            _transform.position += direction.normalized * _speed * Time.deltaTime;
        }
    }
}