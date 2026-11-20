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
        public void TheBakedRun_IsInPlace_AndItsPlantedFootSweepsBackAtTheClipSpeed()
        {
            HordeArtConfig config = Config();
            int n = config.vertexCount;
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(config.vat, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            Assert.IsFalse(request.hasError, "VAT readback");
            var pixels = request.GetData<Vector4>();

            int frames = config.run.count;
            float dt = 1f / config.run.frameRate;
            // Per frame, the lowest vertex (a foot on the ground) and how fast it moves back along the model's forward (z).
            var sweep = new float[frames];
            Vector3 middleFirst = Vector3.zero, middleLast = Vector3.zero;
            for (int f = 0; f < frames; f++)
            {
                int row = config.run.start + f;
                int next = config.run.start + (f + 1) % frames;
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
            }

            Assert.Less(Mathf.Abs(middleLast.z - middleFirst.z), 0.25f * config.runClipSpeed * dt * frames,
                "in place: the body does not stride off over the loop (root motion taken out of the pose)");

            // The stance: the best four frames in a row of the foot sweeping back. On the ground it must sweep at the speed
            // the monster moves when the clip plays at 1x, or it slides.
            float best = 0f;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int k = 0; k < 4; k++)
                    sum += sweep[(f + k) % frames];
                best = Mathf.Max(best, sum / 4f);
            }

            Debug.Log($"[ZombieRun] planted foot sweeps back at {best:F2} m/s, clip played for {config.runClipSpeed:F2} m/s");
            Assert.That(best, Is.InRange(config.runClipSpeed * 0.85f, config.runClipSpeed * 1.15f),
                "the planted foot sweeps back at the clip's speed: no sliding at 1x");
        }
    }
}
