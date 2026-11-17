using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A model baked by the Voxelizer: on start each part gets a VoxelBody from the baked data, under
    /// that part's transform, so a turret still turns and wheels still spin. Nothing is voxelized here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelModel : MonoBehaviour
    {
        [SerializeField] private VoxelModelAsset _data;

        private readonly List<VoxelBody> _bodies = new();

        public IReadOnlyList<VoxelBody> Bodies => _bodies;

        public void SetData(VoxelModelAsset data) => _data = data;

        private void Start() => Build();

        public void Build()
        {
            if (_bodies.Count > 0 || _data == null)
                return;

            foreach (VoxelModelAsset.Part part in _data.parts)
            {
                Transform root = string.IsNullOrEmpty(part.path) ? transform : transform.Find(part.path);
                if (root == null)
                {
                    Debug.LogWarning($"[VoxelModel] {name}: no part '{part.path}'; it is left out.", this);
                    continue;
                }

                int3 size = new int3(part.size.x, part.size.y, part.size.z);
                NativeArray<byte> voxels = VoxelModelAsset.Decode(part.runs, size.x * size.y * size.z, Allocator.Persistent);
                var palette = new NativeArray<Color32>(_data.palette, Allocator.Persistent);
                var go = new GameObject("Voxels") { layer = root.gameObject.layer };
                go.transform.SetParent(root, false);
                VoxelBody body = go.AddComponent<VoxelBody>();
                body.Init(size, voxels, palette, _data.Settings(part, root.gameObject.layer));
                _bodies.Add(body);
            }
        }
    }
}
