using System;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Horde
{
    /// <summary>
    /// Draws many copies of one mesh with one <see cref="Graphics.RenderMeshIndirect"/> call: per-instance
    /// <see cref="HordeInstance"/> data in a structured <see cref="GraphicsBuffer"/> the
    /// <c>Game/HordeVAT</c> shader reads by instance id. A ring of instances (corpses) is drawn from
    /// <c>ringStart</c> on, wrapping at the buffer's capacity. No managed allocations per frame.
    /// </summary>
    public sealed class HordeRenderer : IDisposable
    {
        private static readonly ProfilerMarker RenderMarker = new("Horde.Render");
        private static readonly int InstancesId = Shader.PropertyToID("_HordeInstances");
        private static readonly int CorpseId = Shader.PropertyToID("_HordeCorpse");
        private static readonly int RingStartId = Shader.PropertyToID("_HordeRingStart");
        private static readonly int RingCapacityId = Shader.PropertyToID("_HordeRingCapacity");
        private static readonly int TimeId = Shader.PropertyToID("_HordeTime");
        private static readonly int CorpseTimesId = Shader.PropertyToID("_HordeCorpseTimes");

        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly GraphicsBuffer _instances;
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private readonly MaterialPropertyBlock _props = new();
        private uint _lastCount = uint.MaxValue;

        /// <param name="corpse">Static mesh (no VAT), drawn as a ring; see <see cref="SetCorpseTimes"/>.</param>
        public HordeRenderer(Mesh mesh, Material material, int capacity, bool corpse = false)
        {
            _mesh = mesh != null ? mesh : throw new ArgumentNullException(nameof(mesh));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
            Capacity = capacity;
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, HordeInstance.STRIDE);
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _argsData[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0),
                startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = mesh.GetBaseVertex(0)
            };
            _props.SetBuffer(InstancesId, _instances);
            _props.SetFloat(CorpseId, corpse ? 1f : 0f);
            _props.SetFloat(RingCapacityId, capacity);
        }

        public int Capacity { get; }

        /// <summary>The crowd casts shadows (off by default: measured cost, see the horde spec).</summary>
        public bool CastShadows { get; set; }

        public bool ReceiveShadows { get; set; }

        public int Layer { get; set; }

        /// <summary>Copies <paramref name="data"/> into the instance buffer at <paramref name="start"/>.</summary>
        public void Upload(NativeArray<HordeInstance> data, int sourceStart, int start, int count)
        {
            if (count <= 0)
                return;
            using (RenderMarker.Auto())
                _instances.SetData(data, sourceStart, start, count);
        }

        /// <summary>Corpse timing for the shader: lie <paramref name="lie"/> s, then sink <paramref name="depth"/> m over <paramref name="sink"/> s.</summary>
        public void SetCorpseTimes(float lie, float sink, float depth) =>
            _props.SetVector(CorpseTimesId, new Vector4(lie, sink, depth, 0f));

        /// <summary>Draws <paramref name="count"/> instances (from <paramref name="ringStart"/>, wrapping) this frame.</summary>
        public void Draw(int count, Bounds worldBounds, int ringStart = 0, float time = 0f)
        {
            if (count <= 0)
                return;

            using (RenderMarker.Auto())
            {
                uint n = (uint)Mathf.Min(count, Capacity);
                if (n != _lastCount)
                {
                    _argsData[0].instanceCount = n;
                    _args.SetData(_argsData);
                    _lastCount = n;
                }

                _props.SetFloat(RingStartId, ringStart);
                _props.SetFloat(TimeId, time);
                var rp = new RenderParams(_material)
                {
                    worldBounds = worldBounds,
                    matProps = _props,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = ReceiveShadows,
                    layer = Layer
                };
                Graphics.RenderMeshIndirect(rp, _mesh, _args);
            }
        }

        public void Dispose()
        {
            _instances?.Dispose();
            _args?.Dispose();
        }
    }
}
