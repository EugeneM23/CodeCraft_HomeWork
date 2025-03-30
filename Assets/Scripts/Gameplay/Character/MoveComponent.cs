using UnityEngine;

namespace Gameplay
{
    public class MoveComponent : MonoBehaviour
    {
        [SerializeField] private float _speed = 5;

        public void Move(Vector3 direction) => transform.position += direction.normalized * _speed * Time.deltaTime;
    }
}