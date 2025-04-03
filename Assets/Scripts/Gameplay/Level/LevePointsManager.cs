using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public class LevePointsManager : MonoBehaviour
    {
        private Transform[] _allPoints;
        public int Count => _allPoints.Length;

        private void Awake()
        {
            GetAllAttackPointsFromParent();
        }

        private void GetAllAttackPointsFromParent()
        {
            _allPoints = gameObject.transform
                .Cast<Transform>()
                .Select(go => go.transform)
                .ToArray();
        }

        public Vector3 GetPoint()
        {
            if (_allPoints.Length == 0)
                throw new InvalidOperationException();

            int rand = Random.Range(0, _allPoints.Length);
            return _allPoints[rand].position;
        }
    }
}