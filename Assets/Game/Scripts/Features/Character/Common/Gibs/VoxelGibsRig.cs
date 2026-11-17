using System;
using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>A body's skin and bones, found by the names a VoxelGibsConfig gives: what VoxelGibs cuts.</summary>
    public sealed class VoxelGibsRig
    {
        public SkinnedMeshRenderer Skin;
        public Transform Skeleton;
        public Renderer[] Hide;
        public SkinnedVoxelizer.Part[] Parts;
        public Transform SpineFrom;
        public Transform SpineTo;
        public Transform LeftMarker;

        /// <summary>The skin material's colour: Regax's M_Rapax 1 is a plain Standard with no texture.</summary>
        public Color SkinColor
        {
            get
            {
                Material material = Skin != null ? Skin.sharedMaterial : null;
                return material != null && material.HasProperty("_Color") ? material.color : Color.gray;
            }
        }

        public static VoxelGibsRig Resolve(SkinnedMeshRenderer skin, Transform skeleton, Renderer[] hide, VoxelGibsConfig config)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);

            Transform Find(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                if (bones.TryGetValue(name, out Transform bone))
                    return bone;
                Debug.LogWarning($"[VOXEL GIBS] no bone '{name}' under {skeleton.name}");
                return null;
            }

            var parts = new SkinnedVoxelizer.Part[config.parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var found = new List<Transform>();
                foreach (string name in config.parts[i].bones ?? Array.Empty<string>())
                {
                    Transform bone = Find(name);
                    if (bone != null)
                        found.Add(bone);
                }

                parts[i] = new SkinnedVoxelizer.Part { Bones = found.ToArray(), Side = config.parts[i].side };
            }

            return new VoxelGibsRig
            {
                Skin = skin,
                Skeleton = skeleton,
                Hide = hide ?? Array.Empty<Renderer>(),
                Parts = parts,
                SpineFrom = Find(config.spineFrom),
                SpineTo = Find(config.spineTo),
                LeftMarker = Find(config.leftMarker)
            };
        }

        /// <summary>
        /// The plane down the spine that cuts the torso in halves, its normal towards the body's left
        /// (the side LeftMarker is on), whatever pose the body died in.
        /// </summary>
        public Plane MidPlane()
        {
            Vector3 point = SpineFrom != null ? SpineFrom.position : Skeleton.position;
            Vector3 spine = SpineFrom != null && SpineTo != null ? SpineTo.position - SpineFrom.position : Skeleton.up;
            Vector3 normal = Vector3.Cross(spine, Skeleton.forward);
            if (normal.sqrMagnitude < 1e-6f)
                normal = Skeleton.right;
            normal.Normalize();
            if (LeftMarker != null && Vector3.Dot(LeftMarker.position - point, normal) < 0f)
                normal = -normal;
            return new Plane(normal, point);
        }
    }
}
