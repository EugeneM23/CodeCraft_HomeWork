using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Horde.Tests
{
    /// <summary>
    /// The zombie runs at its run clip's own root speed (the user: no foot sliding): the baked run (ZombieHordeArtConfig) is
    /// in place, its planted foot sweeps back at the speed the clip is played for (runClipSpeed), and the crawler moves at
    /// that speed at its size, so the clip plays at 1x and the foot stays put on the ground.
    /// </summary>
    public class HordeZombieRunTests
    {
        private const string CONFIG = "Assets/Game/Horde/Art/ZombieHordeArtConfig.asset";

        private static HordeArtConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(CONFIG);
            Assert.IsNotNull(config, CONFIG);
            return config;
        }

        [Test]
        public void TheCrawler_MovesAtTheRunsRootSpeed_AtItsSize()
        {
            HordeArtConfig config = Config();
            HordeKind crawler = HordeKind.Crawler;
            Assert.That(crawler.Speed, Is.EqualTo(config.runClipSpeed * crawler.RenderScale).Within(0.02f),
                "speed = root speed at render scale 1 × the crawler's render scale: the run plays at 1x");
        }

        [Test]
        public void TheBakedRun_IsInPlace_AndItsPlantedFootSweepsBackAtTheClipSpeed() =>
            PlantedSweep(Config().run, Config().runClipSpeed, "run");

        [Test]
        public void TheBakedCrawl_IsInPlace_AndWhatIsOnTheGroundSweepsBackAtItsSpeed()
        {
            HordeArtConfig config = Config();
            Assert.Greater(config.crawl.count, 0, "the crawl row is baked");
            Assert.Greater(config.crawlClipSpeed, 0.1f);
            Assert.Less(config.crawlClipSpeed, config.runClipSpeed, "crawling is slower than running");
            PlantedSweep(config.crawl, config.crawlClipSpeed, "crawl");
        }

        private static void PlantedSweep(HordeClip clipRow, float clipSpeed, string what)
        {
            HordeArtConfig config = Config();
            int n = config.vertexCount;
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(config.vat, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            Assert.IsFalse(request.hasError, "VAT readback");
            var pixels = request.GetData<Vector4>();

            int frames = clipRow.count;
            float dt = 1f / clipRow.frameRate;
            // Per frame, the lowest vertex (a foot on the ground) and how fast it moves back along the model's forward (z).
            var sweep = new float[frames];
            var lift = new float[frames];
            Vector3 middleFirst = Vector3.zero, middleLast = Vector3.zero;
            for (int f = 0; f < frames; f++)
            {
                int row = clipRow.start + f;
                int next = clipRow.start + (f + 1) % frames;
                int low = 0;
                float y = float.MaxValue;
                Vector3 middle = Vector3.zero;
                for (int v = 0; v < n; v++)
                {
                    Vector4 p = pixels[row * n + v];
                    middle += (Vector3)p;
                    if (p.y < y)
                    {
                        y = p.y;
                        low = v;
                    }
                }

                if (f == 0) middleFirst = middle / n;
                if (f == frames - 1) middleLast = middle / n;
                sweep[f] = -(pixels[next * n + low].z - pixels[row * n + low].z) / dt;
                lift[f] = (pixels[next * n + low].y - pixels[row * n + low].y) / dt;
            }

            Assert.Less(Mathf.Abs(middleLast.z - middleFirst.z), 0.25f * clipSpeed * dt * frames,
                "in place: the body does not stride off over the loop (root motion taken out of the pose)");

            // The stance: four frames in a row of whatever is lowest staying down (it does not lift or drop). On the ground it
            // must sweep back at the speed the monster moves when the clip plays at 1x, or it slides; the stance closest to it.
            float best = float.NaN;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                bool down = true;
                for (int k = 0; k < 4; k++)
                {
                    sum += sweep[(f + k) % frames];
                    down &= Mathf.Abs(lift[(f + k) % frames]) < 0.3f * clipSpeed + 0.05f;
                }

                if (down && (float.IsNaN(best) || Mathf.Abs(sum / 4f - clipSpeed) < Mathf.Abs(best - clipSpeed)))
                    best = sum / 4f;
            }

            Assert.IsFalse(float.IsNaN(best), "something stays on the ground for four frames");

            Debug.Log($"[Zombie {what}] planted foot sweeps back at {best:F2} m/s, clip played for {clipSpeed:F2} m/s");
            Assert.That(best, Is.InRange(clipSpeed * 0.85f, clipSpeed * 1.15f),
                "the planted foot sweeps back at the clip's speed: no sliding at 1x");
        }
    }
}
