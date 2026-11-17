using Game.Horde;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>The level grid's second read of a changed rect (a broken-off wall piece falls after the first read).</summary>
    public sealed class LevelNavTests
    {
        private static HordeNavGrid FlatGrid()
        {
            var grid = new HordeNavGrid(HordeNavGridSettings.Cover(new float2(-10f, -10f), new float2(10f, 10f), 0.5f));
            using var heights = new NativeArray<float>(grid.CellCount, Allocator.Temp);
            grid.SetHeights(heights);
            return grid;
        }

        [Test]
        public void AChangedRect_IsReadNow_AndAgainAfterTheDelay()
        {
            using HordeNavGrid grid = FlatGrid();
            using var nav = new LevelNav(grid);

            nav.MarkChanged(new Bounds(Vector3.zero, Vector3.one));
            Assert.Greater(grid.DirtyRectCount, 0, "read now");
            Assert.AreEqual(1, nav.PendingRereads, "and once more later");

            nav.ScheduleUpdate(0.1f);
            Assert.IsTrue(grid.IsUpdating);
            nav.CompleteUpdate();

            nav.ScheduleUpdate(LevelNav.REREAD_DELAY - 0.2f);
            Assert.IsFalse(grid.IsUpdating, "not before the delay");
            Assert.AreEqual(1, nav.PendingRereads);

            nav.ScheduleUpdate(0.2f);
            Assert.IsTrue(grid.IsUpdating, "the second read");
            Assert.AreEqual(0, nav.PendingRereads);
            nav.CompleteUpdate();
        }

        [Test]
        public void ManyChanges_TheQueueStaysBounded()
        {
            using HordeNavGrid grid = FlatGrid();
            using var nav = new LevelNav(grid);

            for (int i = 0; i < 100; i++)
                nav.MarkChanged(new Bounds(new Vector3(-9f + i * 0.18f, 0f, 0f), Vector3.one * 0.2f));
            Assert.LessOrEqual(nav.PendingRereads, 32);

            nav.ScheduleUpdate(LevelNav.REREAD_DELAY + 0.01f);
            Assert.AreEqual(0, nav.PendingRereads, "every change read again, the merged ones too");
            nav.CompleteUpdate();
        }
    }
}
