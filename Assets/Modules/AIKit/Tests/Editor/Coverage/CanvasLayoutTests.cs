using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The geometry of the Brain's wires (GraphLayout, no canvas): a wire nobody placed never leaves from the point of a
    /// wire the user placed on the same side; a decision above or on the left goes round in a lane of its own; little
    /// room — the lanes share it; and the plain helpers (which side faces a box, where a point is on a side, the rim).
    /// </summary>
    public sealed class CanvasLayoutTests
    {
        private const float SPACING = 14f;

        // The Brain's frame of the step-90 tests: xMax 20, y 300…460
        private static readonly Rect Frame = new(-300f, 300f, 320f, 160f);

        [Test]
        public void GraphLayout_BrainExits_AFreeWireBesideAPlacedOne_LeavesAPointOfItsOwn()
        {
            var decisions = new List<Rect> { new(200f, -250f, 300f, 140f), new(200f, 330f, 300f, 140f) };
            var stored = new List<(FrameSide Side, float Along)?> { (FrameSide.Right, 0.5f), null };

            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, decisions, SPACING, stored);

            Assert.AreEqual(new Vector2(20f, 380f), exits[0].Point, "The placed wire leaves where the user put it");
            Assert.AreEqual(FrameSide.Right, exits[1].Side);
            Assert.AreNotEqual(380f, exits[1].Point.y, "The free one leaves from a point of its own");
            Assert.AreNotEqual(exits[0].Lane, exits[1].Lane, "…and runs in a lane of its own");
        }

        // ---------- out of the top, out of the left ----------

        private static readonly Rect Box = new(0f, 0f, 300f, 100f);

        [Test]
        public void GraphLayout_BrainExits_ADecisionAboveTheFrame_GoesRoundInALane()
        {
            var decision = new Rect(50f, -300f, 200f, 100f);
            var into = new Vector2(50f, -300f + GraphLayout.IN_PIN_Y);

            GraphLayout.BrainExit exit = GraphLayout.BrainExits(Box, new List<Rect> { decision }, SPACING)[0];

            Assert.AreEqual(FrameSide.Top, exit.Side);
            Assert.AreEqual(new Vector2(150f, 0f), exit.Point, "The middle of the top");
            Assert.AreEqual(decision.xMin - SPACING, exit.Lane, "Its pin is left of the point: round, in a lane left of it");
            Assert.AreEqual(Box.yMin - SPACING, exit.Turn);
            CollectionAssert.AreEqual(new[]
            {
                new Vector2(150f, 0f), new Vector2(150f, -SPACING), new Vector2(36f, -SPACING), new Vector2(36f, into.y), into
            }, GraphLayout.BrainWirePath(exit, into));
        }

        [Test]
        public void GraphLayout_BrainExits_APinRightOfTheTopPoint_GoesStraight()
        {
            var decision = new Rect(250f, -300f, 200f, 100f);
            var into = new Vector2(250f, -300f + GraphLayout.IN_PIN_Y);

            GraphLayout.BrainExit exit = GraphLayout.BrainExits(Box, new List<Rect> { decision }, SPACING)[0];

            Assert.AreEqual(FrameSide.Top, exit.Side);
            Assert.AreEqual(150f, exit.Lane, "Straight up from its point");
            CollectionAssert.AreEqual(new[] { new Vector2(150f, 0f), new Vector2(150f, into.y), into },
                GraphLayout.BrainWirePath(exit, into));
        }

        [Test]
        public void GraphLayout_BrainExits_ADecisionOnTheLeft_RunsLeftOfItsPin()
        {
            var decision = new Rect(-600f, 0f, 200f, 100f);
            var into = new Vector2(-600f, GraphLayout.IN_PIN_Y);

            GraphLayout.BrainExit exit = GraphLayout.BrainExits(Box, new List<Rect> { decision }, SPACING)[0];

            Assert.AreEqual(FrameSide.Left, exit.Side);
            Assert.AreEqual(new Vector2(0f, 50f), exit.Point);
            Assert.AreEqual(-600f - SPACING, exit.Lane);
            List<Vector2> path = GraphLayout.BrainWirePath(exit, into);
            CollectionAssert.AreEqual(new[] { new Vector2(0f, 50f), new Vector2(-614f, 50f), new Vector2(-614f, into.y), into },
                path);
            for (int i = 1; i < path.Count; i++)
                Assert.IsTrue(path[i].x == path[i - 1].x || path[i].y == path[i - 1].y, $"Stretch {i}: level or upright");
        }

        [Test]
        public void GraphLayout_BrainExits_LittleRoom_TheLanesShareIt()
        {
            var decisions = new List<Rect>
            {
                new(40f, -250f, 300f, 140f), new(40f, 330f, 300f, 140f), new(40f, 900f, 300f, 140f)
            };

            GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(Frame, decisions, SPACING);

            float step = Mathf.Min(SPACING, (40f - Frame.xMax) / 4f);
            CollectionAssert.AreEquivalent(new[] { Frame.xMax + step, Frame.xMax + 2f * step, Frame.xMax + 3f * step },
                exits.Select(exit => exit.Lane), "The room between the frame and the pins, shared");
            foreach (GraphLayout.BrainExit exit in exits)
                Assert.IsTrue(exit.Lane > Frame.xMax && exit.Lane < 40f);
        }

        // ---------- the plain helpers ----------

        [Test]
        public void GraphLayout_Facing_SideOf_OnRim_PointOn_EdgeCases()
        {
            Assert.AreEqual(FrameSide.Right, GraphLayout.Facing(Box, new Rect(200f, 20f, 200f, 50f)), "Overlapping: by its middle");
            Assert.AreEqual(FrameSide.Left, GraphLayout.Facing(Box, new Rect(-150f, 20f, 200f, 50f)));
            Assert.AreEqual(FrameSide.Bottom, GraphLayout.Facing(Box, new Rect(50f, 200f, 100f, 50f)));

            Assert.AreEqual(FrameSide.Left, GraphLayout.SideOf(Box, Vector2.zero, out float along), "A corner: the side wins");
            Assert.AreEqual(0f, along);

            Assert.IsTrue(GraphLayout.OnRim(new Rect(0f, 0f, 10f, 10f), new Vector2(5f, 5f), 10f), "A box smaller than its rim is all rim");
            Assert.IsFalse(GraphLayout.OnRim(Box, new Vector2(500f, 500f), 10f), "Far outside is no rim");

            Assert.AreEqual(new Vector2(300f, 100f), GraphLayout.PointOn(Box, FrameSide.Right, 1.5f), "Clamped to the end");
            Assert.AreEqual(Vector2.zero, GraphLayout.PointOn(Box, FrameSide.Top, -1f));
            Assert.AreEqual(Box.center, GraphLayout.PointOn(Box, FrameSide.None, 0.5f));

            var pin = new Rect(10f, 10f, 16f, 18f);
            Assert.AreEqual(GraphLayout.LinkSpot(pin, 0, 18f), GraphLayout.LinkSpot(pin, -3, 18f), "No index above the first");
        }
    }
}
