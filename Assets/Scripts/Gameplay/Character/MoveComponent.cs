using System;
using UnityEngine;

namespace Gameplay
{
    public interface ImoveCondition
    {
        public bool Invoke(Vector3 position);
    }

    public class PlayerOnScreeenCondition : ImoveCondition
    {
        public bool Invoke(Vector3 position)
        {
            Vector3 viewPortPosition = Camera.main.WorldToViewportPoint(position);
            if (viewPortPosition.x < 0 || viewPortPosition.x > 1 || viewPortPosition.y < 0 || viewPortPosition.y > 1)
                return false;
            
            return true;
        }
    }

    [Serializable]
    public class MoveComponent
    {
        [SerializeField] private Transform _transform;
        [SerializeField] private float _speed = 5;

        public void Move(Vector3 direction)
        {
            _transform.position += direction.normalized * _speed * Time.deltaTime;
        }

        public void SetSpeed(float speed)
        {
            _speed = speed;
        }
    }
}