using System;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Colours of a body that bleeds: skin outside, meat inside, veins of blood in the meat. Meat
    /// and blood are wet — glossy in the Game/Voxel shader (wetness rides in the alpha).
    /// </summary>
    [Serializable]
    public struct FleshPalette
    {
        public const int FIRST_SKIN = 1;
        /// <summary>Skin slots: a textured skin's colours (SkinAtlas.SLOTS), or VARIANTS shades of one colour.</summary>
        public const int SKIN_SLOTS = SkinAtlas.SLOTS;
        public const int FIRST_MEAT = FIRST_SKIN + SKIN_SLOTS;
        public const int MEAT_SLOTS = 4;
        public const int FIRST_BLOOD = FIRST_MEAT + MEAT_SLOTS;
        public const int BLOOD_SLOTS = 2;

        /// <summary>The skin's texture as voxel colours: each skin voxel takes the colour at the UV it was cut at (SkinnedVoxelizer). Null: <see cref="Skin"/>.</summary>
        [NonSerialized] public SkinAtlas Atlas;

        public Color Skin;
        [Range(0f, 0.3f)] public float SkinNoise;
        public Color[] Meat;
        [Range(0f, 1f)] public float MeatWetness;
        public Color[] Blood;
        [Range(0f, 1f)] public float BloodWetness;

        /// <summary>Share of the meat voxels that are blood.</summary>
        [Range(0f, 1f)] public float BloodShare;

        public NativeArray<Color32> BuildPalette(Allocator allocator)
        {
            var palette = new NativeArray<Color32>(256, allocator);
            if (Atlas != null)
                for (int i = 0; i < Atlas.Colours.Length && i < SKIN_SLOTS; i++)
                    palette[FIRST_SKIN + i] = MeshVoxelizer.ToVertexColor(Atlas.Colours[i]);
            else
                MeshVoxelizer.FillShades(palette, FIRST_SKIN, Skin, SkinNoise);
            for (int i = 0; i < MEAT_SLOTS; i++)
                palette[FIRST_MEAT + i] = MeshVoxelizer.WithWetness(
                    MeshVoxelizer.ToVertexColor(Pick(Meat, i, new Color(0.5f, 0.1f, 0.1f))), MeatWetness);
            for (int i = 0; i < BLOOD_SLOTS; i++)
                palette[FIRST_BLOOD + i] = BloodVoxel(i);
            return palette;
        }

        /// <summary>A blood colour as a voxel or a drop carries it, wetness included.</summary>
        public Color32 BloodVoxel(int index) =>
            MeshVoxelizer.WithWetness(MeshVoxelizer.ToVertexColor(Pick(Blood, index, Color.red)), BloodWetness);

        private static Color Pick(Color[] colors, int index, Color fallback) =>
            colors == null || colors.Length == 0 ? fallback : colors[index % colors.Length];
    }
}
