using UnityEngine;

namespace Gameplay
{
    public sealed class LevelBounds : MonoBehaviour
    {
        [SerializeField] private Transform leftBorder;

        [SerializeField] private Transform rightBorder;

        [SerializeField] private Transform downBorder;

        [SerializeField] private Transform topBorder;

        public bool InBounds(Vector3 position)
        {
            bool x = position.x > leftBorder.position.x && position.x < rightBorder.position.x;
            bool y = position.y > downBorder.position.y && position.y < topBorder.position.y;
            
            return x && y;
        }
    }
}