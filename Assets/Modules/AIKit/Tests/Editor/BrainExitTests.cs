using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The Brain's wires do not run together (step 90, C10; spec 4.13, 10.10): each leaves the Brain's frame from a point
    /// of its own on the side facing its decision, spread in the order of the decisions, each in a lane of its own; a
    /// point the user wired from is kept (with the graph, through writes); the Brain's frame holds the Brain alone, and
    /// taking the frame away leaves the Brain.
    /// </summary>
    public sealed class BrainExitTests
    {
        private const float SPACING = 14f;

        // The Brain's frame on the left, three decisions in a column to its right — above, level, below
        private static readonly Rect Frame = new(-300f, 300f, 320f, 160f);

        private static readonly List<Rect> Column = new()
        {
            new Rect(200f, -250f, 300f, 140f),
            new Rect(200f, 330f, 300f, 140f),
            new Rect(200f, 900f, 300f, 140f)
        };

        [Test]
        public void BrainExits_DecisionsRightOfTheFrame_LeaveTheRightSide_SpreadInTheirOrder()
        {
            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, Column, SPACING);

            Assert.AreEqual(3, exits.Length);
            Assert.IsTrue(exits.All(exit => exit.Side == FrameSide.Right), "The side that faces them");
            Assert.IsTrue(exits.All(exit => Mathf.Approximately(exit.Point.x, Frame.xMax)), "On the frame's edge");
            Assert.Less(exits[0].Point.y, exits[1].Point.y, "In the order the decisions stand: no two cross where they leave");
            Assert.Less(exits[1].Point.y, exits[2].Point.y);
            Assert.AreEqual(Frame.y + Frame.height / 4f, exits[0].Point.y, 0.01f, "Spread evenly along the side");
            Assert.AreEqual(Frame.y + Frame.height * 3f / 4f, exits[2].Point.y, 0.01f);
        }

        [Test]
        public void BrainExits_EveryWire_HasALaneOfItsOwn_BetweenTheFrameAndTheDecisions()
        {
            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, Column, SPACING);

            Assert.AreEqual(3, exits.Select(exit => exit.Lane).Distinct().Count(), "No two wires run down one line");
            foreach (GraphLayout.BrainExit exit in exits)
            {
                Assert.Greater(exit.Lane, Frame.xMax, "Out of the frame");
                Assert.Less(exit.Lane, Column[0].xMin, "Before the decisions");
            }

            // A wire going up runs nearer the frame than one leaving lower: none crosses the other
            Assert.Less(exits[0].Lane, exits[2].Lane);
        }

        [Test]
        public void BrainExits_ADecisionAbove_LeavesTheTop_OneOnTheLeft_LeavesTheLeft()
        {
            var decisions = new List<Rect>
            {
                new(-250f, -200f, 200f, 120f),       // over the frame
                new(-900f, 320f, 300f, 140f),        // left of it
                new(300f, 350f, 300f, 140f)          // right of it
            };

            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, decisions, SPACING);

            Assert.AreEqual(FrameSide.Top, exits[0].Side);
            Assert.AreEqual(Frame.yMin, exits[0].Point.y, 0.01f);
            Assert.AreEqual(FrameSide.Left, exits[1].Side);
            Assert.AreEqual(Frame.xMin, exits[1].Point.x, 0.01f);
            Assert.AreEqual(FrameSide.Right, exits[2].Side);
        }

        [Test]
        public void BrainExits_APointTheUserPut_IsKept_TheOthersSpreadOnTheirOwn()
        {
            var stored = new List<(FrameSide Side, float Along)?> { null, (FrameSide.Bottom, 0.75f), null };

            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, Column, SPACING, stored);

            Assert.AreEqual(FrameSide.Bottom, exits[1].Side, "Where it was let go");
            Assert.AreEqual(new Vector2(Frame.xMin + Frame.width * 0.75f, Frame.yMax), exits[1].Point);
            Assert.AreEqual(FrameSide.Right, exits[0].Side);
            Assert.AreEqual(Frame.y + Frame.height / 3f, exits[0].Point.y, 0.01f, "Two free wires share the right side");
            Assert.AreEqual(Frame.y + Frame.height * 2f / 3f, exits[2].Point.y, 0.01f);
        }

        [Test]
        public void BrainWirePath_AtRightAngles_FromItsExit_AlongItsLane_IntoThePin()
        {
            GraphLayout.BrainExit exit = GraphLayout.BrainExits(Frame, Column, SPACING)[0];
            var into = new Vector2(200f, -217f);

            List<Vector2> path = GraphLayout.BrainWirePath(exit, into);

            Assert.AreEqual(exit.Point, path[0]);
            Assert.AreEqual(into, path[^1]);
            for (int i = 1; i < path.Count; i++)
            {
                bool level = Mathf.Approximately(path[i].y, path[i - 1].y);
                bool upright = Mathf.Approximately(path[i].x, path[i - 1].x);
                Assert.IsTrue(level || upright, $"Stretch {i} is never slanted");
            }

            Assert.IsTrue(path.Any(point => Mathf.Approximately(point.x, exit.Lane)), "Down its own lane");
        }

        [Test]
        public void SideOf_TheNearestSide_AndHowFarAlongIt()
        {
            Assert.AreEqual(FrameSide.Right, GraphLayout.SideOf(Frame, new Vector2(Frame.xMax - 2f, Frame.y + 40f), out float along));
            Assert.AreEqual(40f / Frame.height, along, 0.001f);
            Assert.AreEqual(FrameSide.Top, GraphLayout.SideOf(Frame, new Vector2(Frame.x + 80f, Frame.yMin + 1f), out along));
            Assert.AreEqual(0.25f, along, 0.001f);
            Assert.IsTrue(GraphLayout.OnRim(Frame, new Vector2(Frame.xMax - 3f, Frame.center.y), 10f));
            Assert.IsFalse(GraphLayout.OnRim(Frame, Frame.center, 10f), "The middle is no rim");
        }

        // ---------- the document ----------

        [Test]
        public void Document_TheExitOfABrainWire_IsKeptWithTheGraph()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var document = new GraphDocument(asset);
                AgentState brain = document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
                AgentState routine = document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(400f, 0f));
                Transition wire = document.Connect(brain, routine);

                document.SetExit(wire, FrameSide.Bottom, 0.4f);

                var reopened = new GraphDocument(asset);
                Transition read = reopened.States().First(state => state.IsBrain).Transitions.Single();
                Assert.IsTrue(reopened.TryExitOf(read, out FrameSide side, out float along));
                Assert.AreEqual(FrameSide.Bottom, side);
                Assert.AreEqual(0.4f, along, 0.0001f);

                reopened.SetExit(read, FrameSide.None, 0f);
                Assert.IsFalse(new GraphDocument(asset).TryExitOf(
                    new GraphDocument(asset).States().First(state => state.IsBrain).Transitions.Single(), out _, out _),
                    "Taken off: spread with the others again");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Document_TheBrainsFrame_HoldsTheBrainAlone_AndRemovingItLeavesTheBrain()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var document = new GraphDocument(asset);
                AgentState brain = document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
                AgentState wait = document.AddStateWithTask(null, "aikit.task.wait", new Vector2(400f, 0f));
                document.EnsureBrainFrames();
                GraphFrame frame = asset.Frames.Single(found => found.Brain == brain.Id);
                frame.Members.Add(wait.Id);             // an older graph's: a block kept inside the Brain's frame

                document.EnsureBrainFrames();
                CollectionAssert.IsEmpty(frame.Members, "Nothing but the Brain is a part of its frame");
                Assert.AreEqual("Brain", frame.Title);

                document.RemoveFrame(frame);
                Assert.IsTrue(document.States().Contains(brain), "Taking the frame away does not delete the Brain");
                Assert.IsFalse(asset.Frames.Contains(frame));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
