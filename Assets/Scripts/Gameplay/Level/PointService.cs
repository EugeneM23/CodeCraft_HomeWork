using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public class PointService : MonoBehaviour
    {
        private Transform[] _allPoints;

        private void Awake()
        {
            _allPoints = gameObject.transform
                .Cast<Transform>()
                .Select(go => go.transform)
                .ToArray();
        }

        public Vector3 NextPoint()
        {
            if (_allPoints.Length == 0)
                throw new InvalidOperationException();

            int rand = Random.Range(0, _allPoints.Length);
            return _allPoints[rand].position;
        }
    }
}