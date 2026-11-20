using System;
using System.Collections.Generic;
using Game.Voxels;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Game
{
    /// <summary>
    /// A body dies into voxels: its skin is hidden, the pose it died in becomes voxels (skin, meat,
    /// veins of blood), cut into clusters of parts by the same seam rules the Maya gibs use, each
    /// cluster a rigid VoxelBody thrown away from the killing hit. Torn seams and the wound spray
    /// blood drops, and torn pieces drip as they fly. Crumbs and drops lie all game; the last
    /// <see cref="Corpses"/> corpses lie, their bodies reused for the next deaths (VoxelCorpses).
    /// </summary>
    /// <remarks>
    /// <para>A shell's blast also bites into the fresh pieces: ExplosionUseCase kills (DamageInRadius)
    /// before it carves voxels (VoxelDamage.Blast), so nothing here carves.</para>
    /// <para>A death is spread over three frames so no frame pays for all of it (perf debt, the kill frame
    /// was 15-23 ms; Docs/Reports/2026-11-18-perf-fix.md):</para>
    /// <list type="number">
    /// <item>the death itself (<see cref="Burst"/>): the pose is read, the voxelization jobs are scheduled
    /// and the skin's animator is stopped, so the skin stays shown in exactly the pose that is voxelized;</item>
    /// <item>the next frame (<see cref="DeathsPerFrame"/> deaths at most): the jobs are taken, the pieces made
    /// switched off; the voxel world builds their meshes and colliders at the end of that frame;</item>
    /// <item>the frame after: the skin hides and the pieces show, launch, split, drip and bleed in one go.</item>
    /// </list>
    /// <para>The skin stands still in its death pose for those two frames (~33 ms), then the flesh takes its
    /// place: the same voxels, palette, split, throw and blood as when it all happened in one frame.</para>
    /// </remarks>
    public static class VoxelGibs
    {
        public const string NAME = "Flesh";

        /// <summary>
        /// Most deaths taken a step further each frame (built, or shown): a blast that kills six spreads their
        /// flesh over a few frames, the bodies holding their pose until their turn.
        /// </summary>
        public static int DeathsPerFrame = 2;

        /// <summary>Deaths the pools are made for at level load (pieces, drips, buffers): beyond it they grow.</summary>
        public const int RESERVED_DEATHS = 8;

        /// <summary>
        /// Corpses that lie at once (VoxelCorpses): a death past it takes the oldest corpse's bodies, which goes.
        /// Read when a level is made ready (<see cref="Prepare"/>).
        /// </summary>
        public static int Corpses = 20;

        /// <summary>
        /// Bodies made ahead per corpse at level load: a Regax comes apart in 1-7 pieces (4 or so most often) and a
        /// scrap or two split off them. Fewer than a death needs, and the oldest corpse goes early.
        /// </summary>
        public static int BodiesPerCorpse = 7;

        /// <summary>
        /// The chunk layouts (32-voxel chunks along x, y, z) the bodies made ahead are filled with, in turn: a body's
        /// chunks are reused by a piece cut into as many (VoxelBody.Init), so the spares follow the pieces' sizes: in
        /// VoxelSandbox 80% of the pieces and the scraps took one chunk, 17% two, 3% four.
        /// </summary>
        public static int3[] SpareLayouts = { new(1, 1, 1), new(1, 1, 1), new(1, 1, 1), new(1, 1, 1), new(1, 1, 1), new(1, 2, 1), new(1, 2, 2) };

        /// <summary>Box colliders each chunk of a spare is given: a Regax piece's chunk took 20 (median), 64 (p99), 77 at most (10 cm cells).</summary>
        public static int SpareBoxes = 80;

        /// <summary>Bodies made and filled a frame at level load: well under the 2 ms a frame a level load may take.</summary>
        private const int PRIME_PER_FRAME = 4;

        private static bool s_warmedUp;

        // Steps of a death, to find where its time goes (perf debt, Docs/Reports/2026-11-17-perf-baseline.md).
        private static readonly ProfilerMarker VoxelizeMarker = new("Death.Voxelize");
        private static readonly ProfilerMarker CarcassMarker = new("Death.Carcass");
        private static readonly ProfilerMarker LaunchMarker = new("Death.Launch");
        private static readonly ProfilerMarker BloodMarker = new("Death.Blood");
        private static readonly ProfilerMarker WarmUpMarker = new("Death.WarmUp");
        private static readonly ProfilerMarker StepMarker = new("Death.Step");

        private static readonly List<int> Held = new();
        private static readonly Func<float> Roll = () => Random.value;

        /// <summary>The blood palette of each config as voxel colours, made once (the drips keep it and never change it).</summary>
        private static readonly Dictionary<VoxelGibsConfig, Color32[]> BloodByConfig = new();

        /// <summary>The goo of each config (blood and meat) as voxel colours, made once.</summary>
        private static readonly Dictionary<VoxelGibsConfig, Color32[]> GooByConfig = new();

        /// <summary>The seams of each config as the rules take them, made once.</summary>
        private static readonly Dictionary<VoxelGibsConfig, SeamLink[]> LinksByConfig = new();

        /// <summary>
        /// Pieces of flesh still in their first flight: damped while they rise (a sluggish, sticky lift that hangs at
        /// the top), let go once they come down (a heavy drop). Taken out when they fall, settle or after a while.
        /// </summary>
        /// <summary>Skin colours of a torn body's chunks (filled per death).</summary>
        private static readonly Color32[] s_skin = new Color32[2];

        private static readonly List<Rigidbody> Flying = new(64);
        private static readonly List<float> FlyingSince = new(64);

        /// <summary>Linear damping of a piece while it rises, and the longest (s) it is watched.</summary>
        private const float RISE_DAMPING = 2f;
        private const float MAX_RISE_TIME = 3f;

        /// <summary>Layers the flesh never touches: the tank drives through it (the tracks burst what they run over).</summary>
        private const int UNTOUCHED_LAYERS = 1 << PhysicsLayers.Character;

        /// <summary>A death on its way: pooled with its buffers.</summary>
        private sealed class Death
        {
            public readonly SkinnedVoxelizer.Pending Voxels = new();
            public readonly List<VoxelCarcass.Piece> Pieces = new();
            public int[] Cluster = Array.Empty<int>();
            public VoxelGibsRig Rig;
            public VoxelGibsConfig Config;

            /// <summary>The bodies it takes (VoxelCorpses), from the build on.</summary>
            public VoxelCorpses.Corpse Corpse;
            public KillingHit Hit;
            public Bounds Body;
            public bool Built;
            public int Frame;
        }

        private static readonly List<Death> Queue = new();
        private static readonly Stack<Death> Spare = new();
        private static bool s_hooked;
        private static bool s_quitHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Held.Clear();
            BloodByConfig.Clear();
            GooByConfig.Clear();
            Flying.Clear();
            FlyingSince.Clear();
            LinksByConfig.Clear();
            Queue.Clear();
            Spare.Clear();
            DeathsPerFrame = 2;
            Corpses = 20;
            BodiesPerCorpse = 7;
            s_warmedUp = false;
            s_quitHooked = false;
            SetHook(false);
        }

        /// <summary>Deaths read but not yet shown.</summary>
        public static int Pending => Queue.Count;

        /// <summary>
        /// The body dies into flesh: the pose is read and voxelized on workers now, the flesh takes the body's
        /// place two frames on (see the remarks). False when the pose cannot be voxelized: the body stays as it is.
        /// </summary>
        public static bool Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit)
        {
            Death death = Begin(rig, config, hit, false);
            if (death == null)
                return false;

            // Held in the pose that was read: the skin stays shown until the flesh is ready.
            if (rig.Animator != null)
                rig.Animator.enabled = false;

            Queue.Add(death);
            SetHook(true);
            return true;
        }

        /// <summary>
        /// Every death in the queue played through now, whatever frame it is (tests, and a level that goes away).
        /// </summary>
        public static void Flush()
        {
            while (Queue.Count > 0)
            {
                Death death = Queue[0];
                Queue.RemoveAt(0);
                if (!death.Built)
                    Build(death);
                Show(death, false);
                Return(death);
            }
        }

        /// <summary>
        /// One death played through and thrown away at once, so the first death of a fight does not pay for compiling
        /// its code. The loading's WarmUpCodeOperation compiles the game's own assemblies, but not the engine's managed
        /// code on the way (meshes, bodies, components), nor the Burst jobs' first schedule (their reflection data and,
        /// in the editor, the Burst compile). The body is voxelized in the pose it stands in; the pieces are built,
        /// launched, split and given their drips; then every voxel body made on the way is destroyed before the frame
        /// ends. The skin stays shown and no blood flies. Also fills the pools a death takes from, so the deaths of a
        /// fight allocate nothing of their own. Meant for level load: about the cost of one death.
        /// </summary>
        public static void WarmUp(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            if (rig == null || rig.Skin == null || config == null)
                return;

            using (WarmUpMarker.Auto())
            {
                PrepareLevel(config);

                IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
                int before = bodies.Count;
                Transform root = rig.Skin.transform;
                Death death = Begin(rig, config, new KillingHit(root.position - root.forward, 0f, false, 0f), true);
                if (death != null)
                {
                    Build(death);
                    Show(death, true);
                    // Its bodies were never built: the pool fills them before a death takes them.
                    VoxelCorpses.Abandon(death.Corpse, true);
                    Return(death);
                }

                // Anything else made since (registered at the end of the list): the corpse's bodies are put away already.
                for (int i = bodies.Count - 1; i >= before; i--)
                    if (bodies[i] != null)
                        Object.DestroyImmediate(bodies[i].gameObject);
            }
        }

        /// <summary>
        /// A body of this kind comes into the level (its behaviour's Init): the level's corpse bodies are made ready
        /// over the next frames if they are not yet (every level: they go with its scene), and the first body of the
        /// session plays a death through (<see cref="WarmUp"/>), so the first death of a fight compiles nothing.
        /// </summary>
        public static void Prepare(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            if (config == null || !Application.isPlaying)
                return;
            PrepareLevel(config);
            if (s_warmedUp)
                return;
            s_warmedUp = true;
            WarmUp(rig, config);
        }

        /// <summary>The settings every piece of flesh is made with.</summary>
        private static VoxelBodySettings PieceSettings(VoxelGibsConfig config)
        {
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
            return settings;
        }

        /// <summary>The pools of the death (once) and the corpse bodies of the active scene (once a level).</summary>
        private static void PrepareLevel(VoxelGibsConfig config)
        {
            Reserve(config);
            Scene scene = SceneManager.GetActiveScene();
            if (VoxelCorpses.IsPreparedFor(scene))
                return;
            // Made over the next frames by the step (Prime), while no death waits.
            VoxelCorpses.Prepare(scene, Corpses, Corpses * BodiesPerCorpse, PieceSettings(config), SpareLayouts, SpareBoxes);
        }

        /// <summary>The pools a death takes from, made for <see cref="RESERVED_DEATHS"/> deaths at once.</summary>
        private static void Reserve(VoxelGibsConfig config)
        {
            int parts = config.parts.Length;
            while (Spare.Count + Queue.Count < RESERVED_DEATHS)
            {
                var death = new Death { Cluster = new int[parts] };
                death.Pieces.Capacity = parts;
                Spare.Push(death);
            }

            VoxelCarcass.Reserve(RESERVED_DEATHS * parts);
            VoxelDrip.Reserve(RESERVED_DEATHS * parts);
            if (Queue.Capacity < RESERVED_DEATHS)
                Queue.Capacity = RESERVED_DEATHS;
            if (Held.Capacity < config.seams.Length)
                Held.Capacity = config.seams.Length;
            BloodOf(config);
            LinksOf(config);
            // The step goes into the player loop now: putting it in builds a new loop (garbage), not for a death to do.
            SetHook(true);
        }

        /// <summary>Step 1: reads the pose and schedules the voxels; rolls which seams hold. Null if it cannot be voxelized.</summary>
        private static Death Begin(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit, bool warmUp)
        {
            if (rig == null || rig.Skin == null || config == null)
                return null;

            Death death = Spare.Count > 0 ? Spare.Pop() : new Death();
            death.Rig = rig;
            death.Config = config;
            death.Hit = hit;
            death.Body = rig.Skin.bounds; // the wound is the nearest point of the body as it stood
            death.Built = false;
            death.Frame = Time.frameCount;

            var palette = new FleshPalette
            {
                Skin = config.skinColor.a > 0f ? config.skinColor : rig.SkinColor,
                SkinNoise = config.skinNoise,
                Meat = config.meat,
                MeatWetness = config.meatWetness,
                Blood = config.blood.palette,
                BloodWetness = config.blood.wetness,
                BloodShare = config.bloodShare,
                // A textured skin (the zombie's atlas): the voxels take its colours (the user: the pieces in the zombie's colours).
                Atlas = config.skinFromTexture ? rig.Atlas : null
            };

            VoxelizeMarker.Begin();
            SkinnedVoxelizer.Skeleton segments = rig.Segments ?? (rig.Segments = SkinnedVoxelizer.Skeleton.Of(rig.Parts));
            bool voxelized = SkinnedVoxelizer.Begin(rig.Skin, segments, rig.Parts.Length, rig.MidPlane(), config.voxelSize,
                palette, (uint)Random.Range(1, int.MaxValue), death.Voxels);
            VoxelizeMarker.End();
            if (!voxelized)
            {
                // Nothing would take the body's place: better a corpse that stays than one that vanishes.
                if (!warmUp)
                    Debug.LogWarning($"[VOXEL GIBS] {rig.Skin.name}: the pose could not be voxelized; the body stays as it is.");
                Return(death);
                return null;
            }

            if (death.Cluster.Length != config.parts.Length)
                death.Cluster = new int[config.parts.Length];
            GibRules.RollClusters(LinksOf(config), Roll, Held, death.Cluster);
            if (config.shellTearsLimbs && death.Hit.Blast)
                TearLimbs(config, death.Cluster);
            return death;
        }

        /// <summary>
        /// A shell's kill: every part its own piece, but parts named alike before the '_' (Torso_L, Torso_R) stay one: the
        /// body comes apart into head, arms, legs and torso (VoxelGibsConfig.shellTearsLimbs).
        /// </summary>
        private static void TearLimbs(VoxelGibsConfig config, int[] cluster)
        {
            for (int i = 0; i < cluster.Length; i++)
                cluster[i] = i;
            for (int i = 0; i < cluster.Length; i++)
            {
                string a = config.parts[i].name ?? "";
                int cut = a.IndexOf('_');
                if (cut <= 0)
                    continue;
                for (int j = i + 1; j < cluster.Length; j++)
                {
                    string b = config.parts[j].name ?? "";
                    if (b.Length > cut && b[cut] == '_' && string.CompareOrdinal(a, 0, b, 0, cut) == 0)
                        cluster[j] = cluster[i];
                }
            }
        }

        /// <summary>Step 2: the voxels are taken and cut into pieces, made switched off; the world builds them at the end of the frame.</summary>
        private static void Build(Death death)
        {
            VoxelBodySettings settings = PieceSettings(death.Config);

            CarcassMarker.Begin();
            SkinnedVoxelizer.Result grid = death.Voxels.Complete();
            try
            {
                // The corpse's bodies come from the pool: free ones, or the oldest corpse's.
                death.Corpse = VoxelCorpses.Begin();
                VoxelCarcass.Build(grid, death.Cluster, settings, death.Rig.PieceNames, true, death.Pieces, death.Corpse);
            }
            finally
            {
                grid.Dispose();
                CarcassMarker.End();
            }

            death.Built = true;
            death.Frame = Time.frameCount;
        }

        /// <summary>Step 3: the skin hides, the pieces show and fly, split, drip and bleed.</summary>
        private static void Show(Death death, bool warmUp)
        {
            VoxelGibsRig rig = death.Rig;
            VoxelGibsConfig config = death.Config;
            List<VoxelCarcass.Piece> pieces = death.Pieces;

            if (!warmUp)
                for (int i = 0; i < rig.Hide.Length; i++)
                    if (rig.Hide[i] != null)
                        rig.Hide[i].enabled = false;

            LaunchMarker.Begin();

            GibRules.Throw(config.speeds, death.Hit, out float speed, out float lift, out float spin);
            // A shell's kill carries the pieces and most of the goo on the way it flew (VoxelSpray.ShotGoo, SHOT_PUSH).
            Vector3 shot = death.Hit.Direction;
            Vector3 flatShot = new(shot.x, 0f, shot.z);
            bool along = flatShot.sqrMagnitude > 1e-4f;
            Vector3 push = along ? flatShot.normalized * VoxelSpray.SHOT_PUSH : Vector3.zero;
            Color32[] blood = BloodOf(config);
            SeamLink[] links = LinksOf(config);
            int[] cluster = death.Cluster;

            int broken = 0;
            for (int i = 0; i < links.Length; i++)
                if (cluster[links[i].A] != cluster[links[i].B])
                    broken++;
            int torn = 0;
            for (int p = 0; p < pieces.Count; p++)
                torn += pieces[p].TornCount;

            // What splits off the pieces (SplitLoose) is this corpse's too, from the pool.
            using VoxelCorpses.Scope serving = VoxelCorpses.Serve(death.Corpse);
            for (int p = 0; p < pieces.Count; p++)
            {
                VoxelCarcass.Piece piece = pieces[p];
                if (piece.Body == null)
                    continue;

                piece.Body.gameObject.SetActive(true);
                Vector3 velocity = GibRules.ClusterVelocity(piece.Body.WorldBounds.center, death.Hit.Origin, speed, lift, config.speeds) +
                                   push * Random.Range(0.6f, 1f);
                piece.Body.Launch(VoxelSpray.Lifted(velocity), GibRules.ClusterSpin(spin, piece.Parts));
                piece.Body.SplitLoose();
                if (piece.Body == null || piece.TornCount == 0)
                    continue;

                VoxelDrip.Start(piece.Body.transform, piece.Torn, piece.TornCount, blood, config.blood.dropSize,
                    config.dripTime, config.dripRate);

                if (warmUp)
                    continue;

                // The seams' drops are shared out by how much of the cut each piece carries: sticky strands of goo
                // pulled up out of the torn faces, a few at a face.
                int drops = Mathf.RoundToInt(config.seamDrops * broken * piece.TornCount / (float)torn);
                Color32[] goo = GooOf(config);
                for (int i = 0; i < drops; i += SEAM_STRAND)
                {
                    VoxelCarcass.TornFace face = piece.Torn[Random.Range(0, piece.TornCount)];
                    VoxelSpray.Goo(face.Point, face.Normal * 0.6f + Vector3.up, Mathf.Min(SEAM_STRAND, drops - i), config.gooSpeed * 0.8f,
                        0.35f, goo, config.blood.dropSize, velocity * 0.4f, 0.05f);
                }
            }

            LaunchMarker.End();

            // The flesh never touches the tank; it rises sluggishly, then drops (UpdateFlying).
            float now = Time.time;
            for (int b = 0; death.Corpse != null && b < death.Corpse.BodyCount; b++)
            {
                VoxelBody body = death.Corpse.GetBody(b);
                if (body == null || !body.TryGetComponent(out Rigidbody rigidbody))
                    continue;
                rigidbody.excludeLayers = UNTOUCHED_LAYERS;
                if (warmUp || rigidbody.isKinematic)
                    continue;
                rigidbody.linearDamping = RISE_DAMPING;
                Flying.Add(rigidbody);
                FlyingSince.Add(now);
            }

            if (warmUp)
                return;

            BloodMarker.Begin();
            Vector3 wound = death.Body.ClosestPoint(death.Hit.Origin);
            Vector3 away = wound - death.Hit.Origin;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up;
            // With no way known the goo goes up.
            if (along)
                VoxelSpray.ShotGoo(wound, shot, config.hitDrops, config.gooSpeed, 0.4f, GooOf(config),
                    config.blood.dropSize, default, Mathf.Min(0.35f, death.Body.extents.x), VoxelSpray.HE_SPLASH);
            else
                VoxelSpray.Goo(wound, Vector3.up * 1.6f + away * 0.5f, config.hitDrops, config.gooSpeed, 0.4f, GooOf(config),
                    config.blood.dropSize, default, Mathf.Min(0.35f, death.Body.extents.x));
            BloodMarker.End();

            // A shell's kill tears the body into big pieces (the user): head, torso, arms and legs as clumps of flesh. The
            // voxelized skin of a thin, open low-poly body (the zombie) is too sparse to give them by itself.
            if (config.shellTearsLimbs && death.Hit.Blast)
            {
                Vector3 feet = new(death.Body.center.x, death.Body.min.y, death.Body.center.z);
                Vector3 thrown = along ? flatShot : new Vector3(away.x, 0f, away.z);
                s_skin[0] = config.skinColor.a > 0f ? config.skinColor : rig.SkinColor;
                s_skin[1] = Color.Lerp(s_skin[0], Color.black, 0.2f);
                // Each part in the zombie's own colours where its skin has a texture (SkinAtlas.ChunkSkins).
                SkinAtlas atlas = config.skinFromTexture ? rig.Atlas : null;
                VoxelSpray.Chunks(feet, death.Body.size.y, thrown.sqrMagnitude > 1e-4f ? thrown.normalized : Vector3.zero, s_skin, GooOf(config),
                    atlas?.ChunkSkins(), along ? VoxelSpray.SHOT_PUSH : 0f);
            }
        }

        /// <summary>Drops in a strand of goo out of a torn seam.</summary>
        private const int SEAM_STRAND = 6;

        /// <summary>Flying pieces: damped on the way up, let go on the way down (and when they settle or time out).</summary>
        private static void UpdateFlying()
        {
            float now = Time.time;
            for (int i = Flying.Count - 1; i >= 0; i--)
            {
                Rigidbody rigidbody = Flying[i];
                bool done = rigidbody == null || rigidbody.isKinematic || !rigidbody.gameObject.activeInHierarchy
                            || rigidbody.linearVelocity.y <= 0f || now - FlyingSince[i] > MAX_RISE_TIME;
                if (!done)
                    continue;
                if (rigidbody != null)
                    rigidbody.linearDamping = 0f;
                int last = Flying.Count - 1;
                Flying[i] = Flying[last];
                FlyingSince[i] = FlyingSince[last];
                Flying.RemoveAt(last);
                FlyingSince.RemoveAt(last);
            }
        }

        private static Color32[] GooOf(VoxelGibsConfig config)
        {
            if (!GooByConfig.TryGetValue(config, out Color32[] goo))
            {
                Color32[] blood = BloodOf(config);
                Color[] meat = config.meat ?? Array.Empty<Color>();
                goo = new Color32[blood.Length * 2 + meat.Length];
                for (int i = 0; i < blood.Length; i++)
                {
                    goo[i * 2] = blood[i];
                    goo[i * 2 + 1] = blood[i];
                }

                for (int i = 0; i < meat.Length; i++)
                    goo[blood.Length * 2 + i] = meat[i];
                GooByConfig.Add(config, goo);
            }

            return goo;
        }

        /// <summary>A death that cannot go on (its body went away): what was made of it goes back to the pool.</summary>
        private static void Cancel(Death death)
        {
            death.Voxels.Cancel();
            VoxelCorpses.Abandon(death.Corpse);
            Return(death);
        }

        private static void Return(Death death)
        {
            death.Voxels.Cancel();
            VoxelCarcass.Release(death.Pieces);
            // Shown: the corpse lies until it is the oldest of too many.
            VoxelCorpses.Seal(death.Corpse);
            death.Corpse = null;
            death.Rig = null;
            death.Config = null;
            death.Built = false;
            Spare.Push(death);
        }

        /// <summary>
        /// The start of every frame (EarlyUpdate): takes the deaths a step on, oldest first, at most
        /// <see cref="DeathsPerFrame"/> builds and as many shows; never two steps of one death in a frame.
        /// While no death waits, it makes and fills the level's corpse bodies, <see cref="PRIME_PER_FRAME"/> a frame.
        /// </summary>
        private static void Step()
        {
            if (Flying.Count > 0)
                UpdateFlying();

            if (Queue.Count == 0)
            {
                VoxelCorpses.Prime(PRIME_PER_FRAME);
                return;
            }

            using (StepMarker.Auto())
            {
                int frame = Time.frameCount;
                int built = 0;
                int shown = 0;
                for (int i = 0; i < Queue.Count; i++)
                {
                    Death death = Queue[i];
                    if (death.Rig == null || death.Rig.Skin == null)
                    {
                        Queue.RemoveAt(i--);
                        Cancel(death);
                        continue;
                    }

                    if (death.Frame >= frame)
                        continue;

                    if (!death.Built)
                    {
                        if (built >= DeathsPerFrame)
                            continue;
                        built++;
                        Build(death);
                        continue;
                    }

                    if (shown >= DeathsPerFrame)
                        continue;
                    shown++;
                    Queue.RemoveAt(i--);
                    Show(death, false);
                    Return(death);
                }
            }
        }

        /// <summary>Leaving Play (or the game) with deaths still in the queue: their native buffers are freed.</summary>
        private static void CancelAll()
        {
            for (int i = 0; i < Queue.Count; i++)
                Cancel(Queue[i]);
            Queue.Clear();
        }

        private struct VoxelGibsStep
        {
        }

        /// <summary>Puts (or takes out) the step at the end of EarlyUpdate, as VoxelWorld puts its own at the start.</summary>
        private static void SetHook(bool on)
        {
            if (on == s_hooked)
                return;

            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList == null)
                return;

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                    continue;

                PlayerLoopSystem early = loop.subSystemList[i];
                var systems = new List<PlayerLoopSystem>(early.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                systems.RemoveAll(s => s.type == typeof(VoxelGibsStep));
                if (on)
                    systems.Add(new PlayerLoopSystem { type = typeof(VoxelGibsStep), updateDelegate = Step });

                early.subSystemList = systems.ToArray();
                loop.subSystemList[i] = early;
                PlayerLoop.SetPlayerLoop(loop);
                s_hooked = on;
                break;
            }

            if (on && !s_quitHooked)
            {
                s_quitHooked = true;
                Application.quitting += CancelAll;
            }
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

        private static SeamLink[] LinksOf(VoxelGibsConfig config)
        {
            if (!LinksByConfig.TryGetValue(config, out SeamLink[] links))
            {
                links = new SeamLink[config.seams.Length];
                for (int i = 0; i < links.Length; i++)
                    links[i] = new SeamLink(config.seams[i].a, config.seams[i].b, config.seams[i].hold);
                LinksByConfig.Add(config, links);
            }

            return links;
        }
    }
}
