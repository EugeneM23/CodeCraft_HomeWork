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

        private void Pop()
        {
            _popped = true;
            Vector3 at = _body.WorldBounds.center;

            VoxelSpray.Burst(at, Vector3.up, 90, 2f, 6f, 0.9f, Blood, 0.04f);
            VoxelSpray.Burst(at, Vector3.up, 40, 1f, 4f, 1f, Blood, 0.07f);
            if (_inside != null && _inside.IsCreated)
                _inside.Paint(at, 0.7f, Blood[0], 0.45f);

            var drip = new GameObject("CrewDrip");
            drip.transform.position = at;
            var points = new List<Vector3> { at, at + Vector3.left * 0.1f, at + Vector3.forward * 0.1f };
            drip.AddComponent<VoxelDrip>().Begin(points, Blood, 0.035f, 3f, 12f);
            Destroy(drip, 5f);

            // What is left of him goes as clods: the whole body carved out.
            _body.CarveSphere(at, 1.2f, 3f, 300);
            if (_body != null && _body.IsCreated && _body.SolidCount > 0)
                Destroy(_body.gameObject);
        }
    }
}
