using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Goo sheets (VoxelDebris.GooSheet): the middle of a shell's spray rises as one connected, twisting sheet of goo
    /// cubes that leans along the shot, then breaks up into goo drops. A store of its own, ticked by hand.
    /// </summary>
    public sealed class VoxelGooSheetTests
    {
        private const float STEP = 0.02f;
        private static readonly Color32[] Goo = { new(150, 10, 10, 255), new(110, 6, 8, 255) };

        private VoxelDebris _debris;
        private Material _material;
        private readonly List<Vector3> _cubes = new();

        [SetUp]
        public void SetUp()
        {
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            _debris = new VoxelDebris(_material, 4000, 64);
        }

        [TearDown]
        public void TearDown()
        {
            _debris.Dispose();
            Object.Destroy(_material);
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += STEP)
                _debris.Tick(STEP);
            _debris.Complete();
        }

        [Test]
        public void ASheet_RisesAsOneConnectedSurface_LeaningAlongTheShot()
        {
            var at = new Vector3(0f, 100f, 0f);
            Assert.IsTrue(_debris.GooSheet(at, Vector3.right, 1.3f, Goo));
            Run(0.2f);

            _debris.GetSheetCubes(_cubes);
            Assert.AreEqual(_debris.SheetCubes, _cubes.Count);
            Assert.Greater(_cubes.Count, 100, "the whole sheet still holds together");
            Assert.AreEqual(0, _debris.Count, "nothing has let go yet: no loose drops");

            // Edge to edge: every cube has a neighbour about a cube away (one surface, not a cloud).
            float gap = _cubes.Select(p => _cubes.Where(q => q != p).Min(q => Vector3.Distance(p, q))).Max();
            Assert.Less(gap, 0.2f, "no cube stands off on its own");

            float top = _cubes.Max(p => p.y) - at.y;
            Assert.Greater(top, 0.6f, "it climbs out of the wound");
            float lean = _cubes.Where(p => p.y - at.y > top * 0.6f).Average(p => p.x - at.x);
            Assert.Greater(lean, 0.15f, "it leans and curls along the shot");
            Debug.Log($"[GooSheet] {_cubes.Count} cubes, top {top:F2} m, lean {lean:F2} m, max gap {gap:F3} m");
        }

        [Test]
        public void ASheet_TwistsAsItRises()
        {
            var at = new Vector3(0f, 100f, 0f);
            Assert.IsTrue(_debris.GooSheet(at, Vector3.right, 1.3f, Goo));
            Run(0.2f);
            _debris.GetSheetCubes(_cubes);

            // Rows come in order (16 rows of 9): the line across the bottom row and across a high row face different ways.
            Vector3 low = _cubes[8] - _cubes[0];
            Vector3 high = _cubes[12 * 9 + 8] - _cubes[12 * 9];
            low.y = 0f;
            high.y = 0f;
            Assert.Greater(Vector3.Angle(low, high), 40f, "a ribbon that turns round as it climbs");
        }

        [Test]
        public void ASheet_BreaksUp_RimFirst_IntoGooDrops()
        {
            Assert.IsTrue(_debris.GooSheet(new Vector3(0f, 100f, 0f), Vector3.forward, 1.3f, Goo));
            Run(0.02f);
            int whole = _debris.SheetCubes;

            Run(0.32f);
            Assert.Less(_debris.SheetCubes, whole, "the rim and crest have torn off");
            Assert.Greater(_debris.SheetCubes, 0, "the middle still holds");
            Assert.Greater(_debris.Count, 0, "torn off as goo drops");

            Run(0.3f);
            Assert.AreEqual(0, _debris.SheetCount, "gone after about half a second");
            Assert.AreEqual(whole, _debris.Count, "every cube of it is a goo drop now");
        }

        [Test]
        public void Sheets_AreCapped()
        {
            for (int i = 0; i < VoxelDebris.MAX_SHEETS; i++)
                Assert.IsTrue(_debris.GooSheet(new Vector3(i * 3f, 100f, 0f), Vector3.right, 1.3f, Goo));
            Assert.IsFalse(_debris.GooSheet(new Vector3(40f, 100f, 0f), Vector3.right, 1.3f, Goo), "a burst of kills makes eight at most");
            Assert.IsFalse(_debris.GooSheet(new Vector3(0f, 100f, 0f), Vector3.up, 1.3f, Goo), "no way along the ground, no sheet");
        }
    }
}
