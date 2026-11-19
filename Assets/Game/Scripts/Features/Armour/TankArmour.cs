using System;
using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank's armour: on its root, with the model's layout (TankArmourConfig). It tells which zone of the tank a
    /// hit struck (by the voxel body and its part, the plate's facing in that part's frame and where in the body's
    /// grid), and keeps a tally of what was hit inside: the module and crew damage stub. Shells ask
    /// <see cref="Find"/> for the armour of what they struck.
    /// </summary>
    public sealed class TankArmour : MonoBehaviour
    {
        [SerializeField] private TankArmourConfig _config;

        private readonly Dictionary<string, int> _moduleHits = new();

        /// <summary>Every armour hit on any tank (the lab's readout, sounds, a future damage model).</summary>
        public static event Action<ArmourHit> Hit;

        /// <summary>A module or crewman inside a tank was hit: the tank, the zone's name, the hits it has taken.</summary>
        public static event Action<TankArmour, string, int> ModuleDamaged;

        public TankArmourConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public ArmourRules Rules => _config != null ? _config.Rules : ArmourRules.Default;

        public IReadOnlyDictionary<string, int> ModuleHits => _moduleHits;

        /// <summary>The armour of the tank a collider belongs to, or null.</summary>
        public static TankArmour Find(Collider collider) =>
            collider != null ? collider.GetComponentInParent<TankArmour>() : null;

        /// <summary>
        /// The zone a hit struck. <paramref name="normal"/> is the plate's outward normal (smoothed) in world space.
        /// False when the collider is not one of this tank's voxel bodies. A body no zone covers is a plate of the
        /// config's fallback thickness.
        /// </summary>
        public bool Classify(Collider collider, Vector3 point, Vector3 normal, out string zone, out float thickness,
            out ArmourKind kind)
        {
            zone = null;
            thickness = 0f;
            kind = ArmourKind.Plate;
            VoxelBody body = collider != null ? collider.GetComponentInParent<VoxelBody>() : null;
            if (body == null || !body.transform.IsChildOf(transform))
                return false;

            Transform part = body.transform.parent != null ? body.transform.parent : body.transform;
            ArmourFacing facing = ArmourMath.Facing(part.InverseTransformDirection(normal));
            Vector3 normalized = Normalized(body, point);
            TankArmourConfig.Zone match = _config != null ? _config.Find(body.name, part.name, facing, normalized) : null;
            if (match == null)
            {
                zone = part.name;
                thickness = _config != null ? _config.Fallback : 40f;
                return true;
            }

            zone = match.name;
            thickness = match.thickness;
            kind = match.kind;
            return true;
        }

        /// <summary>A point in a body's grid, 0..1 on each axis.</summary>
        public static Vector3 Normalized(VoxelBody body, Vector3 world)
        {
            Vector3 local = body.transform.InverseTransformPoint(world) - (Vector3)body.Origin;
            Vector3 size = (Vector3)(Unity.Mathematics.float3)body.Size * body.VoxelSize;
            return new Vector3(size.x > 0f ? local.x / size.x : 0f, size.y > 0f ? local.y / size.y : 0f,
                size.z > 0f ? local.z / size.z : 0f);
        }

        /// <summary>Reports a hit on this tank: the readout event, and the module tally for hits inside.</summary>
        public void Report(in ArmourHit hit)
        {
            Hit?.Invoke(hit);
            if (hit.Result.Outcome != ArmourOutcome.ModuleHit || hit.Zone == null)
                return;
            _moduleHits.TryGetValue(hit.Zone, out int count);
            _moduleHits[hit.Zone] = ++count;
            ModuleDamaged?.Invoke(this, hit.Zone, count);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Hit = null;
            ModuleDamaged = null;
        }
    }
}
