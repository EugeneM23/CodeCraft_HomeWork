using System.Collections.Generic;
using AIKit;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// "Who is around" for an elite's senses: the player's vehicle, when it is alive and its hull is within the radius.
    /// No physics query and no tag search (they allocate or scan the scene); the eyes then check the cones and the line
    /// of sight (PhysicsEyes3D). The aim point is the middle of the hull, not the root under it.
    /// </summary>
    public sealed class PlayerTankScanner : ITargetScanner, IAimProbe
    {
        private PlayerTank _tank;

        public PlayerTankScanner(PlayerTank tank = null) => _tank = tank;

        public PlayerTank Tank => _tank;

        public void Bind(PlayerTank tank) => _tank = tank;

        public int Scan(Vector3 center, float radius, List<Transform> results)
        {
            results.Clear();
            if (_tank == null || !_tank.IsPresent)
                return 0;

            if (_tank.DistanceToHull(center) <= radius)
                results.Add(_tank.Transform);
            return results.Count;
        }

        public Vector3 AimPoint(Transform target) =>
            _tank != null && target != null && target == _tank.Transform ? _tank.AimPoint
            : target != null ? target.position : Vector3.zero;
    }
}
