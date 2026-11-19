using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A target tank's crewman (TankDummy): the first voxel a hit takes off him, he pops in blood —
    /// a fan of red drops (VoxelSpray, as a horde monster dies), red crumbs, his khaki in clods
    /// (a carve of the whole body), blood on what is round him and a drip from where he sat (VoxelDrip).
    /// </summary>
    public sealed class TankDummyCrew : MonoBehaviour, IVoxelUpdate
    {
        private static readonly Color32[] Blood =
        {
            new(120, 8, 10, 255), new(150, 14, 14, 255), new(92, 4, 6, 255), new(170, 24, 20, 255)
        };

        private VoxelBody _body;
        private VoxelBody _inside;
        private int _whole;
        private bool _popped;

        public void Watch(VoxelBody body, VoxelBody inside)
        {
            _body = body;
            _inside = inside;
            _whole = body.SolidCount;
        }

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void Update() // perf-ok: ticks itself without the game loop (prototype, demo scene); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelUpdate(Time.deltaTime);
        }

        public void VoxelUpdate(float deltaTime)
        {
            if (_popped || _body == null)
                return;
            if (_body.SolidCount >= _whole)
                return;
            Pop();
        }

        private static void BloodCrumbs(Vector3 at, int count, float minSpeed, float maxSpeed, float size)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null)
                return;
            for (int i = 0; i < count; i++)
            {
                Vector3 heading = (Vector3.up + Random.insideUnitSphere * 0.9f).normalized;
                Color32 color = Blood[Random.Range(0, Blood.Length)];
                if (debris.Spawn(at, heading * Random.Range(minSpeed, maxSpeed), color, size * Random.Range(0.8f, 1.2f)) < 0)
                    break;
            }
        }

        private void Pop()
        {
            _popped = true;
            Vector3 at = _body.WorldBounds.center;

            // Red crumbs that fall and fade, not blood drops: a drop lies as a flat world tile for good, and on a
            // tank that turns (the lab's table) or on its sloped armour those tiles hung over the model. The blood
            // that stays is painted into the voxels round him, so it turns with the tank.
            BloodCrumbs(at, 90, 2f, 6f, 0.04f);
            BloodCrumbs(at, 40, 1f, 4f, 0.07f);
            if (_inside != null && _inside.IsCreated)
                _inside.Paint(at, 0.7f, Blood[0], 0.45f);

            // What is left of him goes as clods: the whole body carved out.
            _body.CarveSphere(at, 1.2f, 3f, 300);
            if (_body != null && _body.IsCreated && _body.SolidCount > 0)
                Destroy(_body.gameObject);
        }
    }
}
