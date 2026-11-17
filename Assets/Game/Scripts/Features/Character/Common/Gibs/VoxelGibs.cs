using System;
using System.Collections.Generic;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Game
{
    /// <summary>
    /// A body dies into voxels: its skin is hidden, the pose it died in becomes voxels (skin, meat,
    /// veins of blood), cut into clusters of parts by the same seam rules the Maya gibs use, each
    /// cluster a rigid VoxelBody thrown away from the killing hit. Torn seams and the wound spray
    /// blood drops, and torn pieces drip as they fly. Pieces, crumbs and drops lie all game.
    /// </summary>
    /// <remarks>
    /// A shell's blast also bites into the fresh pieces: ExplosionUseCase kills (DamageInRadius)
    /// before it carves voxels (VoxelDamage.Blast), so nothing here carves.
    /// </remarks>
    public static class VoxelGibs
    {
        public const string NAME = "Flesh";

        // Steps of the kill frame, to find where its ~20 ms go (perf debt, Docs/Reports/2026-11-17-perf-baseline.md).
        private static readonly ProfilerMarker VoxelizeMarker = new("Death.Voxelize");
        private static readonly ProfilerMarker CarcassMarker = new("Death.Carcass");
        private static readonly ProfilerMarker LaunchMarker = new("Death.Launch");
        private static readonly ProfilerMarker BloodMarker = new("Death.Blood");
        private static readonly ProfilerMarker WarmUpMarker = new("Death.WarmUp");

        // Reused by every death, so the kill frame allocates little beyond what the voxel bodies themselves need.
        private static readonly List<SeamLink> Links = new();
        private static readonly List<int> Held = new();
        private static readonly List<Vector3> TornPoints = new();
        private static readonly Func<float> Roll = () => Random.value;

        /// <summary>The blood palette of each config as voxel colours, made once (the drips keep it and never change it).</summary>
        private static readonly Dictionary<VoxelGibsConfig, Color32[]> BloodByConfig = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Links.Clear();
            Held.Clear();
            TornPoints.Clear();
            BloodByConfig.Clear();
        }

        public static List<VoxelCarcass.Piece> Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit) =>
            Burst(rig, config, hit, false);

        /// <summary>
        /// One death played through and thrown away at once, so the first death of a fight does not pay for compiling
        /// its code. The loading's WarmUpCodeOperation compiles the game's own assemblies, but not the engine's managed
        /// code on the way (meshes, bodies, components), nor the Burst jobs' first schedule (their reflection data and,
        /// in the editor, the Burst compile). The body is voxelized in the pose it stands in; the pieces are built,
        /// launched, split and given their drips; then every voxel body made on the way is destroyed before the frame
        /// ends. The skin stays shown and no blood flies. Meant for level load: about the cost of one death.
        /// </summary>
        public static void WarmUp(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            if (rig == null || rig.Skin == null || config == null)
                return;

            using (WarmUpMarker.Auto())
            {
                IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
                int before = bodies.Count;
                Transform root = rig.Skin.transform;
                Burst(rig, config, new KillingHit(root.position - root.forward, 0f, false, 0f), true);

                // Everything made since: the pieces and what split off them (registered at the end of the list).
                for (int i = bodies.Count - 1; i >= before; i--)
                    if (bodies[i] != null)
                        Object.DestroyImmediate(bodies[i].gameObject);
            }
        }

        private static List<VoxelCarcass.Piece> Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit, bool warmUp)
        {
            Bounds body = rig.Skin.bounds; // before the skin is hidden
            var palette = new FleshPalette
            {
                Skin = rig.SkinColor,
                SkinNoise = config.skinNoise,
                Meat = config.meat,
                MeatWetness = config.meatWetness,
                Blood = config.blood.palette,
                BloodWetness = config.blood.wetness,
                BloodShare = config.bloodShare
            };

            VoxelizeMarker.Begin();
            bool voxelized = SkinnedVoxelizer.Voxelize(rig.Skin, rig.Parts, rig.MidPlane(), config.voxelSize, palette,
                (uint)Random.Range(1, int.MaxValue), out SkinnedVoxelizer.Result grid);
            VoxelizeMarker.End();
            if (!voxelized)
            {
                // Nothing would take the body's place: better a corpse that stays than one that vanishes.
                if (!warmUp)
                    Debug.LogWarning($"[VOXEL GIBS] {rig.Skin.name}: the pose could not be voxelized; the body stays as it is.");
                return new List<VoxelCarcass.Piece>();
            }

            // Hidden only now the flesh is sure to come: still this frame, so it looks the same.
            if (!warmUp)
                foreach (Renderer renderer in rig.Hide)
                    if (renderer != null)
                        renderer.enabled = false;

            Links.Clear();
            for (int i = 0; i < config.seams.Length; i++)
                Links.Add(new SeamLink(config.seams[i].a, config.seams[i].b, config.seams[i].hold));
            int[] cluster = GibRules.RollClusters(config.parts.Length, Links, Roll, Held);

            VoxelBodySettings settings = VoxelBodySettings.Default(config.voxelSize);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = true;
            // 10 cm collision cells, not one per 5 cm voxel: ragged flesh came to hundreds of boxes a
            // piece, the bulk of the kill frame, of the first physics steps and of a long game's growth.
            settings.ColliderCell = config.colliderCell;
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Toughness = config.toughness;
            settings.Density = config.density;
            settings.ChunkSize = 32;
            settings.Settle = true;
            settings.KeepDebris = true;
            settings.FractureSpeed = config.fractureSpeed;

            List<VoxelCarcass.Piece> pieces;
            CarcassMarker.Begin();
            try
            {
                pieces = VoxelCarcass.Build(grid, cluster, settings, $"{rig.Skin.name}_{NAME}");
            }
            finally
            {
                grid.Dispose();
                CarcassMarker.End();
            }

            LaunchMarker.Begin();

            GibRules.Throw(config.speeds, hit, out float speed, out float lift, out float spin);
            Color32[] blood = BloodOf(config);

            int broken = 0;
            for (int i = 0; i < Links.Count; i++)
                if (cluster[Links[i].A] != cluster[Links[i].B])
                    broken++;
            int torn = 0;
            for (int p = 0; p < pieces.Count; p++)
                torn += pieces[p].Torn.Length;

            for (int p = 0; p < pieces.Count; p++)
            {
                VoxelCarcass.Piece piece = pieces[p];
                Vector3 velocity = GibRules.ClusterVelocity(piece.Body.WorldBounds.center, hit.Origin, speed, lift, config.speeds);
                piece.Body.Launch(velocity, GibRules.ClusterSpin(spin, piece.Parts));
                piece.Body.SplitLoose();

                if (piece.Torn.Length == 0)
                    continue;

                // The drip copies the points into its own frame: one list serves every piece.
                TornPoints.Clear();
                for (int i = 0; i < piece.Torn.Length; i++)
                    TornPoints.Add(piece.Torn[i].Point);
                piece.Body.gameObject.AddComponent<VoxelDrip>()
                    .Begin(TornPoints, blood, config.blood.dropSize, config.dripTime, config.dripRate);

                if (warmUp)
                    continue;

                // The seams' drops are shared out by how much of the cut each piece carries.
                int drops = Mathf.RoundToInt(config.seamDrops * broken * piece.Torn.Length / (float)torn);
                for (int i = 0; i < drops; i++)
                {
                    VoxelCarcass.TornFace face = piece.Torn[Random.Range(0, piece.Torn.Length)];
                    VoxelSpray.Burst(face.Point, face.Normal, 1, 2f, 5f, 0.35f, blood, config.blood.dropSize, velocity);
                }
            }

            LaunchMarker.End();

            if (warmUp)
                return pieces;

            BloodMarker.Begin();
            Vector3 wound = body.ClosestPoint(hit.Origin);
            Vector3 away = wound - hit.Origin;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up;
            VoxelSpray.Burst(wound, away + Vector3.up * 0.5f, config.hitDrops, 2f, 5f, 0.35f, blood, config.blood.dropSize);
            BloodMarker.End();

            return pieces;
        }

        private static Color32[] BloodOf(VoxelGibsConfig config)
        {
            if (!BloodByConfig.TryGetValue(config, out Color32[] blood))
            {
                blood = config.blood.ToVoxels();
                BloodByConfig.Add(config, blood);
            }

            return blood;
        }
    }
}
