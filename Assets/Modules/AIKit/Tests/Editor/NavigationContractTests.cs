using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>Stage 3b: a navigator that says "no way" makes the movement task fail by itself — no time-out needed.</summary>
    public class NavigationContractTests
    {
        /// <summary>Accepts the destination, then gives up after a number of steps (a reactive navigator in front of a wall).</summary>
        private sealed class GivingUpNavigator : INavigator
        {
            private readonly int _stepsBeforeGivingUp;
            private int _steps;

            public GivingUpNavigator(int stepsBeforeGivingUp) => _stepsBeforeGivingUp = stepsBeforeGivingUp;

            public NavVerdict Verdict = NavVerdict.Reachable;

            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Status == NavStatus.Following ? 10f : Mathf.Infinity;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                if (Status == NavStatus.Unreachable)
                    return NavVerdict.Unreachable;

                Status = Verdict.CanGo() ? NavStatus.Following : NavStatus.Unreachable;
                return Verdict;
            }

            public void Clear()
            {
                Status = NavStatus.Idle;
                _steps = 0;
            }

            public void Step(float dt)
            {
                if (++_steps >= _stepsBeforeGivingUp)
                    Status = NavStatus.Unreachable;
            }
        }

        private static GraphRig RigWith(INavigator navigator)
        {
            return new GraphRig(navigator: navigator) { ThinkInterval = 0.1f };
        }

        [Test]
        public void MoveTo_NavigatorGivesUp_FailsWithoutATimeOut()
        {
            var navigator = new GivingUpNavigator(10);
            GraphRig rig = RigWith(navigator);
            var move = new MoveToTask(new Vector3(20f, 0f, 0f));
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState go = g.Block(move, "Go");
            g.Do(always, go);
            g.OnFail(go, g.Block(new WaitTask(100f), "GiveUp"));       // its No Path pin
            rig.SetGraph(g.Build());

            rig.StepSeconds(0.5f);

            Assert.AreEqual(TaskStatus.Failure, move.StatusOf(rig.Context));
            Assert.AreEqual("GiveUp", rig.StateName);
        }

        [Test]
        public void DirectNavigator_WithoutABody_IsNoGraph()
        {
            var navigator = new DirectNavigator(null);

            Assert.AreEqual(NavVerdict.NoGraph, navigator.SetDestination(Vector3.one));
            Assert.AreEqual(NavStatus.Idle, navigator.Status);
        }
    }
}
