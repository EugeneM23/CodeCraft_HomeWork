using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A piece just torn off bleeds as it flies: for a short while it drops blood from its torn
    /// faces, so a trail of drops marks its arc on the ground.
    /// </summary>
    /// <remarks>
    /// Two ways in. <see cref="Begin"/> on a VoxelDrip added to the piece (the component goes when
    /// its time is up). <see cref="Start"/> runs the same drip from a pool, ticked by one hidden host:
    /// no component and no array per piece, so a death allocates nothing for its drips.
    /// </remarks>
    public sealed class VoxelDrip : MonoBehaviour, IVoxelUpdate
    {
        /// <summary>Most points one drip drops from (a piece's torn faces, VoxelCarcass.MAX_TORN).</summary>
        public const int MAX_POINTS = VoxelCarcass.MAX_TORN;

        private sealed class Stream
        {
            public readonly Vector3[] Points = new Vector3[MAX_POINTS];
            public int Count;
            public Transform Body;
            public Rigidbody Rigidbody;
            public Color32[] Colors;
            public float Size;
            public float Until;
            public float Interval;
            public float Next;
        }

        private static readonly List<Stream> Live = new();
        private static readonly Stack<Stream> Free = new();
        private static VoxelDrip s_host;

        /// <summary>The drip of this component (Begin); null on the host.</summary>
        private Stream _own;
        private bool _isHost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Live.Clear();
            Free.Clear();
            s_host = null;
        }

        /// <summary>Pooled drips running now (Start).</summary>
        public static int Running => Live.Count;

        /// <summary>Makes the host and <paramref name="streams"/> pooled drips ahead (level load).</summary>
        public static void Reserve(int streams)
        {
            Host();
            while (Free.Count + Live.Count < streams)
                Free.Push(new Stream());
        }

        /// <summary>
        /// <paramref name="body"/> drips from the first <paramref name="count"/> torn faces for
        /// <paramref name="duration"/> s, <paramref name="rate"/> drops a second. The points ride with the body from
        /// here on; the drip ends early if the body goes.
        /// </summary>
        public static void Start(Transform body, VoxelCarcass.TornFace[] faces, int count, Color32[] colors, float size,
            float duration, float rate)
        {
            if (body == null || faces == null || count <= 0 || colors == null || colors.Length == 0)
                return;

            Host();
            Stream stream = Free.Count > 0 ? Free.Pop() : new Stream();
            stream.Count = Mathf.Min(count, Mathf.Min(faces.Length, MAX_POINTS));
            for (int i = 0; i < stream.Count; i++)
                stream.Points[i] = body.InverseTransformPoint(faces[i].Point);
            Set(stream, body, colors, size, duration, rate);
            Live.Add(stream);
        }

        /// <summary>The pooled drips of this body end now (it goes back to a pool, VoxelCorpses).</summary>
        public static void Stop(Transform body)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Stream stream = Live[i];
                if (!ReferenceEquals(stream.Body, body))
                    continue;
                stream.Body = null;
                stream.Rigidbody = null;
                stream.Colors = null;
                Live.RemoveAt(i);
                Free.Push(stream);
            }
        }

        /// <param name="worldPoints">Where the drops come from, now; they ride with this object from here on.</param>
        public void Begin(IReadOnlyList<Vector3> worldPoints, Color32[] colors, float size, float duration, float rate)
        {
            if (worldPoints == null || worldPoints.Count == 0 || colors == null || colors.Length == 0)
            {
                Destroy(this);
                return;
            }

            _own ??= new Stream();
            _own.Count = Mathf.Min(worldPoints.Count, MAX_POINTS);
            for (int i = 0; i < _own.Count; i++)
                _own.Points[i] = transform.InverseTransformPoint(worldPoints[i]);
            Set(_own, transform, colors, size, duration, rate);
        }

        private static void Set(Stream stream, Transform body, Color32[] colors, float size, float duration, float rate)
        {
            stream.Body = body;
            stream.Rigidbody = null;
            stream.Colors = colors;
            stream.Size = size;
            stream.Until = VoxelTicks.Time + duration;
            stream.Interval = 1f / Mathf.Max(0.01f, rate);
            stream.Next = VoxelTicks.Time;
        }

        private static VoxelDrip Host()
        {
            if (s_host != null)
                return s_host;

            var go = new GameObject("VoxelDrips") { hideFlags = HideFlags.HideInHierarchy };
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            s_host = go.AddComponent<VoxelDrip>();
            s_host._isHost = true;
            return s_host;
        }

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void OnDestroy()
        {
            if (s_host != this)
                return;
            s_host = null;
            Live.Clear();
        }

        private void Update() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelUpdate(Time.deltaTime);
        }

        public void VoxelUpdate(float deltaTime)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;

            if (_isHost)
            {
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    Stream stream = Live[i];
                    if (Drip(stream, debris))
                        continue;
                    stream.Body = null;
                    stream.Rigidbody = null;
                    stream.Colors = null;
                    Live.RemoveAt(i);
                    Free.Push(stream);
                }

                return;
            }

            if (_own == null || !Drip(_own, debris))
                Destroy(this);
        }

        /// <summary>Drops what is due; false once the drip is over (time up, or the body gone).</summary>
        private static bool Drip(Stream stream, VoxelDebris debris)
        {
            if (stream.Body == null || stream.Count == 0 || VoxelTicks.Time >= stream.Until)
                return false;
            if (debris == null)
                return true;
            // Looked for until found, as before: a piece gets its Rigidbody when launched.
            if (stream.Rigidbody == null)
                stream.Body.TryGetComponent(out stream.Rigidbody);

            Transform body = stream.Body;
            Rigidbody rigidbody = stream.Rigidbody;
            while (stream.Next <= VoxelTicks.Time)
            {
                stream.Next += stream.Interval;
                Vector3 point = body.TransformPoint(stream.Points[Random.Range(0, stream.Count)]);
                Vector3 velocity = rigidbody != null && !rigidbody.isKinematic ? rigidbody.GetPointVelocity(point) : Vector3.zero;
                debris.Drop(point, velocity + Random.insideUnitSphere * 0.4f, stream.Colors[Random.Range(0, stream.Colors.Length)], stream.Size);
            }

            return true;
        }
    }
}
