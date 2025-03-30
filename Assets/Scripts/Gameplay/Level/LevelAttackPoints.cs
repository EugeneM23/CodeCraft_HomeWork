using System.Linq;
using UnityEngine;

namespace Gameplay
{
    public class LevelAttackPoints : MonoBehaviour
    {
        private Transform[] _allPoints;
        public static LevelAttackPoints Instance;
        public int Count => _allPoints.Length;

        void Start()
        {
            GetAllAttackPointsFromParent();

            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void GetAllAttackPointsFromParent()
        {
            _allPoints = gameObject.transform
                .Cast<Transform>()
                .Select(go => go.transform)
                .ToArray();
        }

        public Transform GetAttackPoint(int index)
        {
            if (index < _allPoints.Length)
                return _allPoints[index];

            return null;
        }
    }
}