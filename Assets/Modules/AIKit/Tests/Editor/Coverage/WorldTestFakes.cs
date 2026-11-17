using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A navigator for the world's tests: straight at the point at the pace it is given; a way or none (CanReach); it
    /// stands — still on the way, as a NavMesh agent pressing against a body stands at Following — once it is within
    /// BlockWithin of BlockAt (somebody stands there), or everywhere while Blocked. It counts the steps it stood instead of
    /// walking and writes down the pace of every step.
    /// </summary>
    internal sealed class WorldWalker : INavigator, INavigationPace
    {
        private readonly TestActuator _body;
        private NavStatus _status;

        public WorldWalker(TestActuator body) => _body = body;

        /// <summary>Whether there is a way to a point (by default — everywhere).</summary>
        public Func<Vector3, bool> CanReach = _ => true;

        /// <summary>It stands wherever it is.</summary>
        public bool Blocked;

        /// <summary>It stands once it is this near BlockAt (0 — never).</summary>
        public float BlockWithin;

        public Vector3 BlockAt;

        /// <summary>Steps it stood instead of walking.</summary>
        public int StoodSteps;

        /// <summary>The pace of every step walked (or stood).</summary>
        public readonly List<float> Paces = new();

        public float Pace { get; set; } = 1f;

        public NavStatus Status => _status;

        public Vector3 Destination { get; private set; }

        public Vector3 SteeringTarget => Destination;

        public float RemainingDistance =>
            _status.IsMoving() ? Vector3.Distance(Flat(_body.Position), Flat(Destination)) : Mathf.Infinity;

        public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

        public NavVerdict SetDestination(Vector3 point)
        {
            Destination = point;
            if (!CanReach(point))
            {
                _status = NavStatus.Unreachable;
                return NavVerdict.Unreachable;
            }

            if (!_status.IsMoving())
                _status = NavStatus.Following;
            return NavVerdict.Reachable;
        }

        public void Clear() => _status = NavStatus.Idle;

        public void Step(float dt)
        {
            if (!_status.IsMoving())
                return;

            Paces.Add(Pace);
            bool stands = Blocked || BlockWithin > 0f && Flat(_body.Position - BlockAt).magnitude <= BlockWithin;
            if (stands)
            {
                StoodSteps++;
                _body.Move(Vector3.zero);
                return;
            }

            _body.Move(Flat(Destination - _body.Position), Pace);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
