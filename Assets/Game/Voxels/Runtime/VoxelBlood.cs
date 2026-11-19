using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Blood soaked into the ground (the user: it becomes part of the ground for good, the tracks spread and mix it, it
    /// never goes). A few shades, each in <see cref="LEVELS"/> steps from fresh to churned: every time the tracks churn it,
    /// it goes a step toward the earth's brown, and the last step stays a visible red-brown. So the ground takes at most
    /// <see cref="SHADES"/> x <see cref="LEVELS"/> palette slots for blood, however much is spilled.
    /// </summary>
    public static class VoxelBlood
    {
        public const int SHADES = 3;
        public const int LEVELS = 4;

        /// <summary>Share of the way to <see cref="Earth"/> each churned step goes: the last step is 45 % earth.</summary>
        private const float DILUTE_STEP = 0.15f;

        /// <summary>Fresh blood soaked in, dark (sRGB): wet, as the alpha says (see MeshVoxelizer.WithWetness).</summary>
        private static readonly Color[] Fresh =
        {
            new(0.27f, 0.018f, 0.014f), new(0.19f, 0.012f, 0.009f), new(0.12f, 0.007f, 0.006f)
        };

        /// <summary>The earth it mixes toward (dark: churned blood reads as dark red-brown, not as pink).</summary>
        private static readonly Color Earth = new(0.25f, 0.15f, 0.08f);

        private static readonly Color32[] s_colours = Build();

        private static Color32[] Build()
        {
            var colours = new Color32[SHADES * LEVELS];
            for (int s = 0; s < SHADES; s++)
            for (int l = 0; l < LEVELS; l++)
            {
                Color c = Color.Lerp(Fresh[s], Earth, l * DILUTE_STEP);
                // Wet when fresh, drier as it is churned in.
                colours[s * LEVELS + l] = MeshVoxelizer.WithWetness(c, Mathf.Lerp(0.4f, 0.15f, l / (LEVELS - 1f)));
            }

            return colours;
        }

        /// <summary>The colour of blood of a shade at a step of churning (0 fresh .. LEVELS - 1).</summary>
        public static Color32 Colour(int shade, int level) =>
            s_colours[math.clamp(shade, 0, SHADES - 1) * LEVELS + math.clamp(level, 0, LEVELS - 1)];

        /// <summary>
        /// Whether a drop of this colour is blood (it soaks into the ground) rather than acid, skin or anything else that
        /// lies as it is: clearly red, not bright.
        /// </summary>
        public static bool IsBlood(float4 color) =>
            color.x > 0.04f && color.x > color.y * 2.2f && color.x > color.z * 2.2f && color.x < 0.85f;

        public static bool IsBlood(Color32 color) => IsBlood(new float4(color.r, color.g, color.b, 255f) / 255f);

        /// <summary>The shade a drop of this colour soaks in as: by how dark it is.</summary>
        public static int ShadeOf(float4 color) => color.x > 0.3f ? 0 : color.x > 0.2f ? 1 : 2;
    }
}
