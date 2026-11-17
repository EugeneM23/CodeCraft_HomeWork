using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Mud cakes onto the tank as it drives through it, and builds up: the more it manoeuvres, the
    /// dirtier it gets, up to caked all over the running gear and well up the hull.
    /// TankTrackMarksBehaviour reports the mud churned up (<see cref="Splash"/>) — far more for
    /// sliding round in a turn than for rolling straight — and so does every cannon shot over mud
    /// (TankMuzzleMudBehaviour). Fresh mud is wet and glossy and dries dull over half a minute.
    ///
    /// Standing still, the mud melts away: once the tank has stopped for a couple of seconds the
    /// dirt drains off, all of it in under a minute.
    ///
    /// Drawn by Game/TankDirt on every renderer whose material has a _Dirt property, through a
    /// property block. Each renderer gets its own frame into the tank's space, taken once at start,
    /// so the pattern sticks to the part — the wheels turn with it, the turret swings with it.
    /// </summary>
    public sealed class TankDirtBehaviour : IEntityInit, IEntityTick
    {
        /// <summary>Seconds fresh mud takes to dry.</summary>
        private const float DRY_TIME = 30f;

        /// <summary>Smallest change in dirt or wetness worth sending to the renderers.</summary>
        private const float EPSILON = 0.004f;

        /// <summary>Seconds standing still before the mud starts to melt.</summary>
        private const float MELT_DELAY = 2f;

        /// <summary>Seconds standing still that melt the dirtiest tank clean.</summary>
        private const float MELT_TIME = 45f;

        /// <summary>Slowest (m/s) the tank counts as moving.</summary>
        private const float STILL_SPEED = 0.3f;

        /// <summary>Slowest turn (rad/s) the tank counts as moving.</summary>
        private const float STILL_TURN = 0.15f;

        private static readonly int DirtId = Shader.PropertyToID("_Dirt");
        private static readonly int WetId = Shader.PropertyToID("_DirtWet");
        private static readonly int FrameId = Shader.PropertyToID("_DirtFrame");
        private static readonly int LowId = Shader.PropertyToID("_DirtLow");
        private static readonly int HighId = Shader.PropertyToID("_DirtHigh");

        private readonly List<Renderer> _renderers = new();
        private MaterialPropertyBlock _block;

        private Rigidbody _body;
        private float _dirt;
        private float _wet;
        private float _still;
        private float _sentDirt = -1f;
        private float _sentWet = -1f;

        public float Dirt => _dirt;

        public void Init(IEntity entity)
        {
            Transform root = entity.GetTransform();
            _body = entity.GetRiggedBody();
            _block = new MaterialPropertyBlock();

            // The tank's bottom and top in its own space, from the meshes themselves: a renderer's
            // world bounds are empty while it is off or not yet drawn, and one bad box there puts the
            // whole tank under mud or none of it.
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            List<Matrix4x4> frames = new();
            float low = float.MaxValue;
            float high = float.MinValue;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(DirtId) ||
                    !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;

                Matrix4x4 frame = toRoot * renderer.transform.localToWorldMatrix;
                _renderers.Add(renderer);
                frames.Add(frame);

                Bounds bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    float y = frame.MultiplyPoint3x4(corner).y;
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
            }

            if (_renderers.Count == 0)
                return;

            for (int r = 0; r < _renderers.Count; r++)
            {
                Renderer renderer = _renderers[r];
                renderer.GetPropertyBlock(_block);
                _block.SetMatrix(FrameId, frames[r]);
                _block.SetFloat(LowId, low);
                _block.SetFloat(HighId, high);
                _block.SetFloat(DirtId, 0f);
                _block.SetFloat(WetId, 0f);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Adds <paramref name="amount"/> of dirt (1 = caked to the top) and wets it.</summary>
        public void Splash(float amount)
        {
            if (amount <= 0f)
                return;

            _dirt = Mathf.Min(1f, _dirt + amount);
            _wet = 1f;
        }

        /// <summary>Sheds a <paramref name="share"/> of the dirt at once — a shot's jolt; 1 leaves the tank clean.</summary>
        public void Shed(float share) => _dirt *= 1f - Mathf.Clamp01(share);

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_renderers.Count == 0)
                return;

            _wet = Mathf.Max(0f, _wet - deltaTime / DRY_TIME);
            Melt(deltaTime);

            if (Mathf.Abs(_dirt - _sentDirt) < EPSILON && Mathf.Abs(_wet - _sentWet) < EPSILON)
                return;

            _sentDirt = _dirt;
            _sentWet = _wet;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(DirtId, _dirt);
                _block.SetFloat(WetId, _wet);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Standing still for <see cref="MELT_DELAY"/> seconds, the mud starts to drain off. Moving
        /// again stops it at once.
        /// </summary>
        private void Melt(float deltaTime)
        {
            if (_body == null || _dirt <= 0f)
                return;

            bool moving = _body.linearVelocity.sqrMagnitude > STILL_SPEED * STILL_SPEED ||
                          Mathf.Abs(_body.angularVelocity.y) > STILL_TURN;
            _still = moving ? 0f : _still + deltaTime;

            if (_still > MELT_DELAY)
                _dirt = Mathf.Max(0f, _dirt - deltaTime / MELT_TIME);
        }
    }
}
