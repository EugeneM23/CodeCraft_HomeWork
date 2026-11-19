using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank model's armour layout: its zones with their nominal thickness, and the shared armour rules. A hit is
    /// matched to the first zone whose part, facing and box fit it, so the order matters: put narrow zones (the
    /// mantlet, the lower front plate) before broad ones (the hull's front).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Tank Armour Config", fileName = "TankArmourConfig")]
    public sealed class TankArmourConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Zone
        {
            [Tooltip("What the lab shows: «ВЛД», «Борт корпуса»…")]
            public string name = "Зона";

            [Tooltip("Which voxel body: matched against the body's own name, then its part root's (HullVisual, TurretVisual, Gun, Track?_*, Wheel_*). '*' and '?' are wildcards.")]
            public string part = "HullVisual";

            [Tooltip("Which way the struck plate faces in its part's frame (+z front).")]
            public ArmourFacing facing = ArmourFacing.Any;

            [Tooltip("The zone's box in the body's grid, 0..1 on each axis (x right, y up, z front).")]
            public Vector3 boxMin = Vector3.zero;

            public Vector3 boxMax = Vector3.one;

            [Tooltip("Nominal thickness, mm.")]
            public float thickness = 50f;

            public ArmourKind kind = ArmourKind.Plate;
        }

        [SerializeField] private ArmourRules _rules = ArmourRules.Default;

        [SerializeField] private List<Zone> _zones = new();

        [Tooltip("Thickness of a struck voxel body no zone covers, mm.")]
        [SerializeField] private float _fallback = 40f;

        public ArmourRules Rules => _rules;

        public IReadOnlyList<Zone> Zones => _zones;

        public float Fallback => _fallback;

        /// <summary>Sets the layout from code (tests, the editor's default layout).</summary>
        public void Set(ArmourRules rules, IEnumerable<Zone> zones, float fallback)
        {
            _rules = rules;
            _zones = new List<Zone>(zones);
            _fallback = fallback;
        }

        /// <summary>
        /// The first zone that fits a hit, or null.
        /// </summary>
        /// <param name="bodyName">The struck voxel body's name.</param>
        /// <param name="partName">Its part root's name (the body's parent).</param>
        /// <param name="facing">The struck plate's facing in the part's frame.</param>
        /// <param name="normalized">The hit in the body's grid, 0..1 on each axis.</param>
        public Zone Find(string bodyName, string partName, ArmourFacing facing, Vector3 normalized)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                Zone zone = _zones[i];
                if (zone == null)
                    continue;
                if (!Wildcard.Match(zone.part, bodyName) && !Wildcard.Match(zone.part, partName))
                    continue;
                if ((zone.facing & facing) == 0 && zone.facing != ArmourFacing.Any)
                    continue;
                if (normalized.x < zone.boxMin.x || normalized.y < zone.boxMin.y || normalized.z < zone.boxMin.z ||
                    normalized.x > zone.boxMax.x || normalized.y > zone.boxMax.y || normalized.z > zone.boxMax.z)
                    continue;
                return zone;
            }

            return null;
        }

        /// <summary>'*' (any run) and '?' (any one character) matching, case-sensitive, whole string.</summary>
        public static class Wildcard
        {
            public static bool Match(string pattern, string text)
            {
                if (string.IsNullOrEmpty(pattern) || text == null)
                    return false;
                return Match(pattern, 0, text, 0);
            }

            private static bool Match(string p, int i, string t, int j)
            {
                while (i < p.Length)
                {
                    char c = p[i];
                    if (c == '*')
                    {
                        for (int k = j; k <= t.Length; k++)
                            if (Match(p, i + 1, t, k))
                                return true;
                        return false;
                    }

                    if (j >= t.Length || (c != '?' && c != t[j]))
                        return false;
                    i++;
                    j++;
                }

                return j == t.Length;
            }
        }
    }
}
