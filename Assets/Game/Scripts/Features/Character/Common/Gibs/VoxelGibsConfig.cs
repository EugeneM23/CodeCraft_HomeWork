using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How a body comes apart in voxels: its parts by bone names, the seams between them, how hard
    /// the pieces fly, what the flesh and blood look like. The defaults are Regax's (the bones and
    /// seams of RegaxGibsBuilder; Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
    /// </summary>
    [Serializable]
    public sealed class VoxelGibsConfig
    {
        [Serializable]
        public sealed class PartSpec
        {
            public string name;
            public string[] bones;

            [Tooltip("0 — the whole part; +1 — only the body's left of the spine plane, −1 — its right.")]
            public int side;
        }

        [Serializable]
        public sealed class SeamSpec
        {
            public int a;
            public int b;
            [Range(0f, 1f)] public float hold;
        }

        [Tooltip("Edge of one voxel, m.")]
        public float voxelSize = 0.05f;

        [Tooltip("Blast radius multiplier on the pieces: a shell bites about 1 m out of them, not half the body.")]
        [Range(0.1f, 1f)] public float toughness = 0.6f;

        [Tooltip("kg/m³, for the mass of the pieces.")]
        public float density = 1000f;

        [Tooltip("Slowest landing (m/s) that breaks a piece: above a fall from the body's height (~5.4).")]
        public float fractureSpeed = 7f;

        [Tooltip("Edge, in voxels, of a piece's collision cell: 2 — 10 cm boxes. 1 gives a box per voxel, " +
                 "hundreds per ragged piece, and makes the kill frame and the first physics steps heavy.")]
        [Min(1)] public int colliderCell = 2;

        [Tooltip("The spine plane (cuts the torso in halves) runs through these two bones…")]
        public string spineFrom = "Rapax_ Pelvis";
        public string spineTo = "Rapax_ Spine2";

        [Tooltip("…and its + side is the one this bone is on.")]
        public string leftMarker = "Rapax_ L UpperArm";

        public PartSpec[] parts =
        {
            new() { name = "Head", bones = new[] { "Rapax_ Neck", "Rapax_ Head", "Rapax_JawLeft", "Rapax_JawMiddle", "Rapax_JawRight" } },
            new() { name = "Arm_L", bones = new[] { "Rapax_ L UpperArm", "Rapax_ L Forearm", "Rapax_ L Hand", "Rapax_ L Finger0", "Rapax_ L Finger1", "Rapax_ L Finger2" } },
            new() { name = "Arm_R", bones = new[] { "Rapax_ R UpperArm", "Rapax_ R Forearm", "Rapax_ R Hand", "Rapax_ R Finger0", "Rapax_ R Finger1", "Rapax_ R Finger2" } },
            new() { name = "Leg_L", bones = new[] { "Rapax_ L Thigh", "Rapax_ L Calf", "Rapax_ L Foot", "Rapax_ L Toe0", "Rapax_ L Toe1", "Rapax_ L Toe2" } },
            new() { name = "Leg_R", bones = new[] { "Rapax_ R Thigh", "Rapax_ R Calf", "Rapax_ R Foot", "Rapax_ R Toe0", "Rapax_ R Toe1", "Rapax_ R Toe2" } },
            new() { name = "Torso_L", side = 1, bones = Torso },
            new() { name = "Torso_R", side = -1, bones = Torso }
        };

        public SeamSpec[] seams =
        {
            new() { a = 5, b = 6, hold = 0.5f },
            new() { a = 5, b = 1, hold = 0.4f },
            new() { a = 6, b = 2, hold = 0.4f },
            new() { a = 5, b = 3, hold = 0.35f },
            new() { a = 6, b = 4, hold = 0.35f },
            new() { a = 5, b = 0, hold = 0.2f },
            new() { a = 6, b = 0, hold = 0.2f }
        };

        public GibSpeeds speeds = new();

        [Header("Flesh")]
        [Tooltip("Meat inside the skin (sRGB), dark red to pink.")]
        public Color[] meat =
        {
            new(0.35f, 0.04f, 0.05f), new(0.55f, 0.10f, 0.12f), new(0.70f, 0.25f, 0.28f), new(0.45f, 0.15f, 0.10f)
        };

        [Range(0f, 1f)] public float meatWetness = 0.6f;

        [Tooltip("Share of the meat that is veins of blood.")]
        [Range(0f, 1f)] public float bloodShare = 0.15f;

        [Tooltip("How far each skin voxel's shade strays from the skin material's colour.")]
        [Range(0f, 0.3f)] public float skinNoise = 0.08f;

        public BloodLook blood = new();

        [Header("Bleeding")]
        [Tooltip("Drops out of each torn seam.")]
        public int seamDrops = 200;

        [Tooltip("Drops out of the killing wound.")]
        public int hitDrops = 300;

        [Tooltip("How long (s) a torn piece drips as it flies, and how many drops a second.")]
        public float dripTime = 0.6f;
        public float dripRate = 150f;

        private static string[] Torso => new[]
        {
            "Rapax_ Pelvis", "Rapax_ Spine", "Rapax_ Spine1", "Rapax_ Spine2", "Rapax_ L Clavicle",
            "Rapax_ R Clavicle", "Rapax_ Tail", "Rapax_ Tail1", "Rapax_ Tail2"
        };
    }
}
