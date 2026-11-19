using System.Collections.Generic;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank gets bloody exactly where the gore hits it, on its voxel skin (VoxelizedModel), the way
    /// TankCrushBehaviour stains it: the death goo of zombies (VoxelSpray.Goo) that flies into the hull is caught
    /// by the debris step (VoxelDebris.SetStainBox: goo never collides with the tank) and splats there. A body part
    /// burst under a track (TankSquashBehaviour, the horde's corpses) bloodies that track only (the user): its voxel
    /// track parts round the spot, its belt (VoxelTrackRuts.AddTrackBlood), which then leaves bloody prints that fade
    /// over some 15 m of driving.
    ///
    /// The stains are taken into the tank's frame as they come (so they land where they hit as it drives on) and laid
    /// <see cref="STAINS_PER_STEP"/> a physics step: a burst of sixty deaths spreads its blood over a few tenths of a
    /// second instead of one frame. The plate keeps the stain for the run (VoxelBody.Paint); the wet clots laid on top
    /// by a squash (VoxelBody.Deposit) are loose like mud, and come off as the tank drives on.
    /// </summary>
    public sealed class TankBloodBehaviour : IEntityInit, IEntityFixedTick, IEntityDispose
    {
        private static readonly Unity.Profiling.ProfilerMarker StainMarker = new("Crush.BloodStains");

        /// <summary>
        /// Stains laid a physics step at most. A Paint itself is cheap, but every skin chunk it touches is rebuilt that
        /// frame: three keep a crush burst under the horde benchmark's p95; a squash's stains go on over a few tenths of a second.
        /// </summary>
        public const int STAINS_PER_STEP = 3;

        /// <summary>
        /// Past <see cref="STAINS_PER_STEP"/>, one more a step for every this many waiting (up to <see cref="MAX_STAINS_PER_STEP"/>):
        /// a squash fountain landing on the deck in half a second (hundreds of drops) is laid in a few tenths, not lost.
        /// </summary>
        private const int BACKLOG_PER_STAIN = 12;
        private const int MAX_STAINS_PER_STEP = 8;

        /// <summary>
        /// Time (ms) a step lays stains for at most, past the first: a splat scans the skin round it (~0.2 ms in the editor), and
        /// the goo of sixty deaths landing on the deck (a thousand drops) must not take the frame. The rest wait.
        /// </summary>
        private const double STEP_BUDGET_MS = 1.5;
        private static readonly System.Diagnostics.Stopwatch s_clock = new();

        /// <summary>A splat this close (m, in the tank's frame) to one still waiting joins it (a bigger splat) instead of queueing.</summary>
        private const float MERGE_DISTANCE = 0.2f;
        /// <summary>How many of the newest waiting stains a splat looks at to join.</summary>
        private const int MERGE_LOOK = 64;
        private const float MAX_MERGED_RADIUS = 0.2f;

        /// <summary>Squashes (track stains) laid a step at most, and how near (m) two under one track join.</summary>
        private const int TRACK_STAINS_PER_STEP = 2;
        private const float TRACK_MERGE_DISTANCE = 1f;

        /// <summary>Size (m) of the splat a goo drop leaves: small, as each costs a scan of the skin round it (drops that land
        /// close join into bigger ones, MAX_MERGED_RADIUS).</summary>
        private const float GOO_SPLAT = 0.15f;

        /// <summary>Share of the voxels in a stain's sphere that take the blood: a speckled splat, not a ball.</summary>
        private const float PAINT_CHANCE = 0.55f;
        private const float CLOT_CHANCE = 0.3f;

        /// <summary>How far (m) past the skin's bounds goo is caught: what the hull would meet a step later.</summary>
        private const float CATCH_PAD = 0.15f;

        /// <summary>Dark reds: fresh, darker, almost black. Few, as every colour takes a palette slot of a skin part.</summary>
        private static readonly Color32[] Blood =
        {
            new Color(0.34f, 0.025f, 0.02f), new Color(0.24f, 0.015f, 0.012f), new Color(0.15f, 0.01f, 0.008f)
        };

        private struct LocalStain
        {
            public Vector3 Local;
            public float Radius;
            public bool Clots;
            public bool Track;
            public float Amount;
        }

        private readonly Rigidbody _body;
        private readonly LocalStain[] _queue = new LocalStain[TankBloodStains.CAPACITY];
        private readonly List<Vector3> _hits = new(64);
        private readonly List<VoxelBody> _near = new(16);
        private VoxelizedModel _skin;
        private VoxelTrackRuts _ruts;
        /// <summary>The skin's track parts (their parent named Track*): the only ones a squash bloodies. Found at the first.</summary>
        private readonly List<VoxelBody> _trackParts = new(8);
        private bool _trackPartsFound;
        private bool _boxReady;
        private int _boxAge;

        /// <summary>Physics steps between remakes of the box (it follows the turret: a tenth of a second is near enough).</summary>
        private const int BOX_EVERY = 5;
        private Bounds _localBox;
        private int _head;
        private int _count;

        /// <summary>Stains laid since start (tests, the debug panel).</summary>
        public int Laid { get; private set; }

        public TankBloodBehaviour(Rigidbody body) => _body = body;

        public void Init(IEntity entity)
        {
            _skin = _body != null ? _body.GetComponent<VoxelizedModel>() : null;
            _ruts = _body != null ? _body.GetComponent<VoxelTrackRuts>() : null;
            TankBloodStains.Clear();
        }

        public void Dispose(IEntity entity)
        {
            VoxelWorld world = VoxelWorld.Instance;
            if (world != null && world.Debris != null)
                world.Debris.ClearStainBox();
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_skin == null || _body == null)
                return;
            // The box follows the skin each step: the turret and its gun turn, and a box made once (gun forward) left the rear
            // deck out when the gun was turned (the user: the tank did not stain everywhere).
            if ((!_boxReady || ++_boxAge >= BOX_EVERY) && TryMakeBox())
                _boxAge = 0;
            if (!_boxReady)
                return;

            using (StainMarker.Auto())
            {
                Transform frame = _body.transform;
                VoxelWorld world = VoxelWorld.Instance;
                VoxelDebris debris = world != null ? world.Debris : null;
                if (debris != null)
                {
                    debris.SetStainBox(frame.TransformPoint(_localBox.center), frame.rotation, _localBox.extents);
                    _hits.Clear();
                    debris.TakeStainHits(_hits);
                    for (int i = 0; i < _hits.Count; i++)
                        TankBloodStains.Splat(_hits[i], GOO_SPLAT);
                }

                // Into the tank's frame as they come: they land where they hit.
                while (TankBloodStains.TryTake(out TankBloodStains.Stain stain))
                    Enqueue(new LocalStain
                    {
                        Local = frame.InverseTransformPoint(stain.Point), Radius = stain.Radius, Clots = stain.Clots,
                        Track = stain.Track, Amount = stain.Amount
                    });

                int budget = Mathf.Min(MAX_STAINS_PER_STEP, STAINS_PER_STEP + _count / BACKLOG_PER_STAIN);
                s_clock.Restart();
                int tracks = 0;
                for (int i = 0; i < budget && _count > 0; i++)
                {
                    if (i > 0 && s_clock.Elapsed.TotalMilliseconds > STEP_BUDGET_MS)
                        break;
                    LocalStain next = _queue[_head];
                    // A squash paints a big sphere of the track's parts: TRACK_STAINS_PER_STEP a step, the rest wait their turn.
                    if (next.Track && tracks >= TRACK_STAINS_PER_STEP)
                        break;
                    if (next.Track)
                        tracks++;
                    _head = (_head + 1) % _queue.Length;
                    _count--;
                    if (next.Track)
                        LayOnTrack(frame.TransformPoint(next.Local), next.Radius, next.Amount);
                    else
                        Lay(frame.TransformPoint(next.Local), next.Radius, next.Clots);
                }
            }
        }

        private void Enqueue(in LocalStain stain)
        {
            // A splat right by one still waiting joins it: a fountain's drops land in clumps, and each costs a skin repaint.
            if (!stain.Track)
            {
                for (int k = 0; k < MERGE_LOOK && k < _count; k++)
                {
                    int j = (_head + _count - 1 - k) % _queue.Length;
                    LocalStain other = _queue[j];
                    if (other.Track || (other.Local - stain.Local).sqrMagnitude > MERGE_DISTANCE * MERGE_DISTANCE)
                        continue;
                    other.Radius = Mathf.Min(MAX_MERGED_RADIUS, Mathf.Max(other.Radius, stain.Radius) * 1.08f);
                    other.Clots |= stain.Clots;
                    _queue[j] = other;
                    return;
                }
            }
            else
            {
                // Squashes under one track a metre apart join: the track is as bloody, its parts painted once.
                for (int k = 0; k < MERGE_LOOK && k < _count; k++)
                {
                    int j = (_head + _count - 1 - k) % _queue.Length;
                    LocalStain other = _queue[j];
                    if (!other.Track || Mathf.Sign(other.Local.x) != Mathf.Sign(stain.Local.x) ||
                        (other.Local - stain.Local).sqrMagnitude > TRACK_MERGE_DISTANCE * TRACK_MERGE_DISTANCE)
                        continue;
                    other.Amount = Mathf.Min(1f, Mathf.Max(other.Amount, stain.Amount) + 0.1f);
                    _queue[j] = other;
                    return;
                }
            }

            if (_count == _queue.Length)
            {
                _head = (_head + 1) % _queue.Length;
                _count--;
            }

            _queue[(_head + _count) % _queue.Length] = stain;
            _count++;
        }

        /// <summary>A squash under a track: that track's voxel parts round the spot, and its belt and prints.</summary>
        private void LayOnTrack(Vector3 point, float radius, float amount)
        {
            if (_ruts != null)
                _ruts.AddTrackBlood(point, amount);

            if (!_trackPartsFound)
            {
                _trackPartsFound = true;
                IReadOnlyList<VoxelBody> parts = _skin.Bodies;
                for (int b = 0; b < parts.Count; b++)
                {
                    VoxelBody part = parts[b];
                    Transform parent = part != null ? part.transform.parent : null;
                    if (parent != null && parent.name.StartsWith("Track"))
                        _trackParts.Add(part);
                }
            }

            Color32 colour = Blood[Random.Range(0, Blood.Length)];
            for (int i = 0; i < _trackParts.Count; i++)
            {
                VoxelBody part = _trackParts[i];
                if (part != null && part.IsCreated && part.WorldBounds.SqrDistance(point) <= radius * radius)
                    part.Paint(point, radius, colour, PAINT_CHANCE);
            }

            Laid++;
        }

        /// <summary>Steps a stain takes toward the tank's middle to find skin under a spot in the air (goo is caught in a box).</summary>
        private const int MARCH_STEPS = 6;

        /// <summary>Steps (of DROP_STEP m) a stain goes down, in the tank's frame, to find the skin under a spot in the air.</summary>
        private const int DROP_STEPS = 18;
        private const float DROP_STEP = 0.12f;

        /// <summary>
        /// The blood on the skin at the spot; where the spot is in the air (the caught box is bigger than the skin), it goes
        /// on down to the first skin under it, as falling goo would have (the deck, a fender, the rear plate), and only
        /// where there is none (goo thrown at the side) toward the tank's middle. Going to the middle first put all
        /// the goo that fell on the deck onto the turret's sides (the user: the tank did not stain everywhere).
        /// </summary>
        private void Lay(Vector3 point, float radius, bool clots)
        {
            Color32 fall = Blood[Random.Range(0, Blood.Length)];
            Vector3 down = -_body.transform.up * DROP_STEP;
            for (int s = 0; s <= DROP_STEPS; s++)
            {
                // Painted only where skin is (a cheap look a few voxels down first): a sphere in the air over the deck met the
                // turret's box and scanned it for nothing at every step (24 ms in a step of a burst of sixty deaths).
                if (!OnSkin(point + down * s))
                    continue;
                // Skin here: laid once, even when it is all blood already (Paint gives 0) - going on down repainted a
                // soaked deck at every step.
                LayAt(point + down * s, radius, fall, clots);
                Laid++;
                return;
            }

            Vector3 middle = _body.transform.TransformPoint(_localBox.center);
            Vector3 toMiddle = middle - point;
            float distance = toMiddle.magnitude;
            Vector3 step = distance > 1e-3f ? toMiddle / distance * (radius * 0.8f) : Vector3.zero;
            Color32 colour = Blood[Random.Range(0, Blood.Length)];
            for (int s = 0; s <= MARCH_STEPS; s++)
            {
                if (!OnSkin(point + step * s))
                    continue;
                LayAt(point + step * s, radius, colour, clots);
                Laid++;
                return;
            }
        }

        /// <summary>Whether skin lies at or a few voxels under a point (VoxelBody.TryGetColor): cheap, no sphere is scanned.</summary>
        private bool OnSkin(Vector3 point)
        {
            IReadOnlyList<VoxelBody> parts = _skin.Bodies;
            for (int b = 0; b < parts.Count; b++)
            {
                VoxelBody part = parts[b];
                if (part != null && part.IsCreated && part.SolidCount > 0 && part.WorldBounds.Contains(point) &&
                    part.TryGetColor(point, out _))
                    return true;
            }

            return false;
        }

        private int LayAt(Vector3 point, float radius, Color32 colour, bool clots)
        {
            _near.Clear();
            IReadOnlyList<VoxelBody> parts = _skin.Bodies;
            for (int b = 0; b < parts.Count; b++)
            {
                VoxelBody part = parts[b];
                if (part != null && part.IsCreated && part.SolidCount > 0 && part.WorldBounds.SqrDistance(point) <= radius * radius)
                    _near.Add(part);
            }

            int painted = 0;
            for (int i = 0; i < _near.Count; i++)
            {
                painted += _near[i].Paint(point, radius, colour, PAINT_CHANCE);
                if (clots)
                    painted += _near[i].Deposit(point, radius * 0.6f, Blood[0], CLOT_CHANCE);
            }

            return painted;
        }

        /// <summary>The skin's bounds in the tank's frame, once it is built: the box goo is caught in.</summary>
        private bool TryMakeBox()
        {
            IReadOnlyList<VoxelBody> parts = _skin.Bodies;
            if (parts.Count == 0)
                return false;

            Transform frame = _body.transform;
            bool any = false;
            var box = new Bounds();
            for (int b = 0; b < parts.Count; b++)
            {
                VoxelBody part = parts[b];
                if (part == null || !part.IsCreated)
                    continue;
                part.ReserveStains();
                Bounds world = part.WorldBounds;
                Vector3 c = world.center;
                Vector3 e = world.extents;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = c + new Vector3((k & 1) != 0 ? e.x : -e.x, (k & 2) != 0 ? e.y : -e.y, (k & 4) != 0 ? e.z : -e.z);
                    Vector3 local = frame.InverseTransformPoint(corner);
                    if (!any)
                    {
                        box = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(local);
                    }
                }
            }

            if (!any)
                return false;
            box.Expand(CATCH_PAD * 2f);
            _localBox = box;
            _boxReady = true;
            return true;
        }
    }
}
