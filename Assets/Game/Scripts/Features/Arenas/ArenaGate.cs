using System;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The way out of an arena: a brick wall across a gap in the far edge. Until the arena is
    /// cleared an unseen barrier stands in the gap (shells and debris go through it, the tank does
    /// not); once open, the tank rams or shoots the wall down and drives through, and a trigger
    /// past the wall says it is gone.
    /// </summary>
    /// <remarks>The object stands at the wall's middle; its trigger box is at the exit.</remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaGate : MonoBehaviour
    {
        /// <summary>Layers the arena's unseen barriers let through: bullets, debris, rubble, thrown pieces.</summary>
        public static readonly LayerMask PassThrough = (1 << 11) | (1 << 12) | (1 << 15) | (1 << 16);

        private BoxCollider _barrier;
        private BoxCollider _exit;
        private Rigidbody _tank;

        public bool IsOpen { get; private set; }

        /// <summary>The tank went into the exit trigger after the gate opened.</summary>
        public event Action Passed;

        public VoxelStructure Wall { get; private set; }

        public void Build(GatePlan plan, Rigidbody tank, int wallLayer)
        {
            _tank = tank;
            Vector3 size = (Vector3)plan.Size * ArenaPlan.VOXEL;

            var wall = new GameObject("Wall") { layer = wallLayer };
            wall.transform.SetParent(transform, false);
            Wall = wall.AddComponent<VoxelStructure>();
            // Built by the caller, with the arena's other structures (VoxelStructure.BuildAll).
            Wall.Configure(VoxelStructure.Shape.Wall, plan.Size, 1);

            // The barrier stands just in front of the wall, taller than the tank can climb.
            var barrier = new GameObject("Barrier") { layer = 2 };
            barrier.transform.SetParent(transform, false);
            _barrier = barrier.AddComponent<BoxCollider>();
            _barrier.center = new Vector3(0f, 3f, -size.z * 0.5f - 0.4f);
            _barrier.size = new Vector3(size.x + 0.4f, 6f, 0.6f);
            _barrier.excludeLayers = PassThrough;

            Vector3 exit = transform.InverseTransformPoint(plan.Exit);
            _exit = gameObject.AddComponent<BoxCollider>();
            _exit.isTrigger = true;
            _exit.center = exit + Vector3.up * 1.5f;
            _exit.size = new Vector3(size.x + 2f, 3f, 2f);
            _exit.enabled = false;
        }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            if (_barrier != null)
                _barrier.enabled = false;
            if (_exit != null)
                _exit.enabled = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOpen || _tank == null || other.attachedRigidbody != _tank)
                return;

            _exit.enabled = false;
            Passed?.Invoke();
        }
    }
}
