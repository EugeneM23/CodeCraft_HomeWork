using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

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

        public static List<VoxelCarcass.Piece> Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit)
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

            if (!SkinnedVoxelizer.Voxelize(rig.Skin, rig.Parts, rig.MidPlane(), config.voxelSize, palette,
                    (uint)Random.Range(1, int.MaxValue), out SkinnedVoxelizer.Result grid))
            {
                // Nothing would take the body's place: better a corpse that stays than one that vanishes.
                Debug.LogWarning($"[VOXEL GIBS] {rig.Skin.name}: the pose could not be voxelized; the body stays as it is.");
                return new List<VoxelCarcass.Piece>();
            }

            // Hidden only now the flesh is sure to come: still this frame, so it looks the same.
            foreach (Renderer renderer in rig.Hide)
                if (renderer != null)
                    renderer.enabled = false;

            var links = new SeamLink[config.seams.Length];
            for (int i = 0; i < links.Length; i++)
                links[i] = new SeamLink(config.seams[i].a, config.seams[i].b, config.seams[i].hold);
            int[] cluster = GibRules.RollClusters(config.parts.Length, links, () => Random.value, new List<int>());

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
            try
            {
                pieces = VoxelCarcass.Build(grid, cluster, settings, $"{rig.Skin.name}_{NAME}");
            }
            finally
            {
                grid.Dispose();
            }

            GibRules.Throw(config.speeds, hit, out float speed, out float lift, out float spin);
            Color32[] blood = config.blood.ToVoxels();

            int broken = 0;
            foreach (SeamLink seam in links)
                if (cluster[seam.A] != cluster[seam.B])
                    broken++;
            int torn = 0;
            foreach (VoxelCarcass.Piece piece in pieces)
                torn += piece.Torn.Length;

            foreach (VoxelCarcass.Piece piece in pieces)
            {
                Vector3 velocity = GibRules.ClusterVelocity(piece.Body.WorldBounds.center, hit.Origin, speed, lift, config.speeds);
                piece.Body.Launch(velocity, GibRules.ClusterSpin(spin, piece.Parts));
                piece.Body.SplitLoose();

                if (piece.Torn.Length == 0)
                    continue;

                var points = new Vector3[piece.Torn.Length];
                for (int i = 0; i < points.Length; i++)
                    points[i] = piece.Torn[i].Point;
                piece.Body.gameObject.AddComponent<VoxelDrip>()
                    .Begin(points, blood, config.blood.dropSize, config.dripTime, config.dripRate);

                // The seams' drops are shared out by how much of the cut each piece carries.
                int drops = Mathf.RoundToInt(config.seamDrops * broken * piece.Torn.Length / (float)torn);
                for (int i = 0; i < drops; i++)
                {
                    VoxelCarcass.TornFace face = piece.Torn[Random.Range(0, piece.Torn.Length)];
                    VoxelSpray.Burst(face.Point, face.Normal, 1, 2f, 5f, 0.35f, blood, config.blood.dropSize, velocity);
                }
            }

            Vector3 wound = body.ClosestPoint(hit.Origin);
            Vector3 away = wound - hit.Origin;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up;
            VoxelSpray.Burst(wound, away + Vector3.up * 0.5f, config.hitDrops, 2f, 5f, 0.35f, blood, config.blood.dropSize);

            return pieces;
        }
    }
}
