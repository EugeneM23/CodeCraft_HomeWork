using AIKit;
using Game.Horde;
using Unity.Collections;
using Unity.Mathematics;
using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>The navigator on a synthetic 20 × 20 m grid of 0.5 m cells, flat at 0, walls where a test raises them.</summary>
    public sealed class VoxelGridNavigatorTests
    {
        private HordeNavGrid _grid;
        private LevelNav _nav;
        private FakeActuator _body;
        private VoxelGridNavigator _navigator;

        [SetUp]
        public void SetUp()
        {
            _grid = new HordeNavGrid(HordeNavGridSettings.Cover(new float2(-10f, -10f), new float2(10f, 10f), 0.5f));
            using (var heights = new NativeArray<float>(_grid.CellCount, Allocator.Temp))
                _grid.SetHeights(heights);
            _nav = new LevelNav(_grid);
            _nav.Paths.Synchronous = true;
            _body = new FakeActuator { Position = new Vector3(-8f, 0f, 0f) };
            _navigator = new VoxelGridNavigator(_body, _nav);
        }

        [TearDown]
        public void TearDown()
        {
            _nav.Dispose();
            _grid.Dispose();
        }

        private void Wall(float x0, float z0, float x1, float z1, float height = 5f)
        {
            int2 a = _grid.ClampCell(new float3(x0, 0f, z0));
            int2 b = _grid.ClampCell(new float3(x1, 0f, z1));
            for (int z = a.y; z <= b.y; z++)
            for (int x = a.x; x <= b.x; x++)
                _grid.SetHeight(new int2(x, z), height);
        }

        [Test]
        public void OpenGround_Found_FollowsTowardTheGoal()
        {
            NavVerdict verdict = _navigator.SetDestination(new Vector3(8f, 0f, 0f));

            Assert.That(verdict, Is.EqualTo(NavVerdict.Reachable));
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Following));
            Assert.That(_navigator.RemainingDistance, Is.EqualTo(16f).Within(0.75f));

            _navigator.Step(0.02f);
            Assert.That(_body.LastMove.x, Is.GreaterThan(0.9f), "the stick points at the goal");
        }

        [Test]
        public void AWallInTheWay_ThePathGoesAroundIt()
        {
            Wall(-1f, -6f, 1f, 6f);
            _navigator.SetDestination(new Vector3(8f, 0f, 0f));

            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Following));
            Assert.That(_navigator.WaypointCount, Is.GreaterThan(2), "a bend round the wall");
            bool aroundTheEnd = false;
            for (int i = 0; i < _navigator.WaypointCount; i++)
                aroundTheEnd |= Mathf.Abs(_navigator.Waypoint(i).z) > 6f;
            Assert.IsTrue(aroundTheEnd, "it goes round an end of the wall");
            Assert.That(_navigator.RemainingDistance, Is.GreaterThan(17f));
        }

        [Test]
        public void GoalWalledIn_Partial_UnreachableOnceWalkedToTheNearest()
        {
            Wall(4f, -3f, 4.5f, 3f);
            Wall(4f, 3f, 9.5f, 3.5f);
            Wall(4f, -3.5f, 9.5f, -3f);
            Wall(9f, -3f, 9.5f, 3f);

            NavVerdict verdict = _navigator.SetDestination(new Vector3(7f, 0f, 0f));

            Assert.That(verdict, Is.EqualTo(NavVerdict.Reachable), "the first answer: it is planning");
            Assert.That(_navigator.SetDestination(new Vector3(7f, 0f, 0f)), Is.EqualTo(NavVerdict.Unreachable));
            Assert.IsTrue(_navigator.IsPartial);
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Following), "it still goes as near as it can");

            Vector3 end = _navigator.Waypoint(_navigator.WaypointCount - 1);
            Assert.IsTrue(end.x < 4f || end.x > 9.5f || Mathf.Abs(end.z) > 3.5f, "the nearest reachable point is outside the walls");
            _body.Position = end;
            _navigator.Step(0.02f);
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Unreachable));
        }

        [Test]
        public void AgentBoxedIn_Unreachable()
        {
            // A ring of wall cells round the agent's own cell
            int2 own = _grid.ClampCell(new float3(-7.75f, 0f, 0.25f));
            _body.Position = _grid.CellCenter(own);
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                if (x != 0 || z != 0)
                    _grid.SetHeight(own + new int2(x, z), 5f);

            _navigator.SetDestination(new Vector3(8f, 0f, 0f));

            Assert.That(_navigator.SetDestination(new Vector3(8f, 0f, 0f)), Is.EqualTo(NavVerdict.Unreachable));
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Unreachable));
        }

        [Test]
        public void OffTheGrid_OffGraph()
        {
            _body.Position = new Vector3(30f, 0f, 0f);

            Assert.That(_navigator.SetDestination(new Vector3(0f, 0f, 0f)), Is.EqualTo(NavVerdict.OffGraph));
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Unreachable));
        }

        [Test]
        public void NoGrid_NoGraph()
        {
            var lost = new VoxelGridNavigator(_body);

            Assert.That(lost.SetDestination(Vector3.zero), Is.EqualTo(NavVerdict.NoGraph));
            Assert.That(lost.Status, Is.EqualTo(NavStatus.Idle));
        }

        [Test]
        public void GridChangesAcrossThePath_ItPlansAgain_AndGoesAround()
        {
            _navigator.SetDestination(new Vector3(8f, 0f, 0f));
            int plans = _navigator.Plans;
            Assert.That(_navigator.WaypointCount, Is.EqualTo(2), "open ground: straight");

            Wall(-1f, -6f, 1f, 6f);                            // a crater wall dug across the way
            _navigator.Step(0.02f);

            Assert.That(_navigator.Plans, Is.EqualTo(plans + 1), "planned again at once");
            Assert.That(_navigator.WaypointCount, Is.GreaterThan(2), "the new path goes round");
        }

        [Test]
        public void GridChangesAwayFromThePath_NoNewPlan()
        {
            _navigator.SetDestination(new Vector3(8f, 0f, 0f));
            int plans = _navigator.Plans;

            Wall(-2f, 6f, 2f, 8f);                             // dug far off the way
            _navigator.Step(0.02f);

            Assert.That(_navigator.Plans, Is.EqualTo(plans));
        }

        [Test]
        public void GoalMoves_PlansAgain_AfterAShortGap()
        {
            _navigator.SetDestination(new Vector3(8f, 0f, 0f));
            int plans = _navigator.Plans;

            _navigator.SetDestination(new Vector3(8f, 0f, 3f));
            Assert.That(_navigator.Plans, Is.EqualTo(plans), "not more often than the gap allows");

            _navigator.Step(VoxelGridNavigator.MIN_REPLAN_GAP);
            _navigator.SetDestination(new Vector3(8f, 0f, 3f));
            Assert.That(_navigator.Plans, Is.EqualTo(plans + 1));
            Vector3 end = _navigator.Waypoint(_navigator.WaypointCount - 1);
            Assert.That(end.z, Is.EqualTo(3f).Within(0.5f));
        }

        [Test]
        public void Queued_PlanningUntilTheServicePumps()
        {
            _nav.Paths.Synchronous = false;

            Assert.That(_navigator.SetDestination(new Vector3(8f, 0f, 0f)), Is.EqualTo(NavVerdict.Reachable));
            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Planning));
            Assert.IsTrue(_navigator.IsPlanning);
            Assert.IsFalse(_nav.Paths.IsRunning, "nothing is scheduled outside the pump");

            _nav.PumpPaths();
            Assert.IsTrue(_nav.Paths.IsRunning, "the first pump schedules the search");
            Assert.IsTrue(_navigator.IsPlanning, "and its result waits for the next frame");

            _nav.PumpPaths();

            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Following));
            Assert.IsFalse(_navigator.IsPlanning);
        }

        [Test]
        public void Clear_Idle()
        {
            _navigator.SetDestination(new Vector3(8f, 0f, 0f));
            _navigator.Clear();

            Assert.That(_navigator.Status, Is.EqualTo(NavStatus.Idle));
            Assert.That(_navigator.RemainingDistance, Is.EqualTo(float.PositiveInfinity));
        }
    }
}
