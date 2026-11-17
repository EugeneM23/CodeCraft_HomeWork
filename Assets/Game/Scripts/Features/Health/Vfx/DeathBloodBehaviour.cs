using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Throws one of the pack's volumetric blood effects where the character died: a random one,
    /// or always the same one when <c>variant</c> is set (targets that show one effect each).
    /// </summary>
    /// <remarks>
    /// The effect is spawned from the pack's own prefab rather than kept switched off inside the
    /// character. The pack has seventeen of them, and an instance saved inside a prefab picks up
    /// whatever its [ExecuteAlways] scripts did to it in the editor (the decal once lost its
    /// material that way and drew as a white box).
    ///
    /// The pack's red is replaced: a MaterialPropertyBlock carries <c>_Color</c> on the gush
    /// (BFX_ManualAnimationUpdate reads the renderer's block back before writing its own frame
    /// values, so the colour stays). The pool gets <c>_TintColor</c> on the material copy
    /// BFX_ShaderProperies makes for it.
    /// </remarks>
    public sealed class DeathBloodBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int FluidColor = Shader.PropertyToID("_Color");
        private static readonly int DecalColor = Shader.PropertyToID("_TintColor");

        private readonly DeathBloodConfig _config;
        private readonly Transform _anchor;
        private readonly int _variant;

        private ISignal<TakeDamageArgs> _deathTakenEvent;

        public DeathBloodBehaviour(DeathBloodConfig config, Transform anchor, int variant = -1)
        {
            _config = config;
            _anchor = anchor;
            _variant = variant;
        }

        public void Init(IEntity entity)
        {
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _deathTakenEvent.Subscribe(this.OnDeath);
        }

        public void Dispose(IEntity entity) => _deathTakenEvent.Unsubscribe(this.OnDeath);

        private void OnDeath(TakeDamageArgs _)
        {
            GameObject[] variants = _config.variants;
            if (variants == null || variants.Length == 0)
                return;

            int index = _variant >= 0 ? _variant % variants.Length : Random.Range(0, variants.Length);
            GameObject prefab = variants[index];
            if (prefab == null)
                return;

            // A random heading, and only around the vertical: the fall is baked into the
            // animation, so a tilted effect would run sideways.
            Vector3 position = _anchor.position + Vector3.up * _config.height;
            Quaternion heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject blood = Object.Instantiate(prefab, position, heading);
            blood.transform.localScale = Vector3.one * _config.scale;

            // Same frame as the spawn, before the pack's GlobalUpdate first runs: the decal reads
            // its own scale on that first update and carries it into the pool.
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in blood.GetComponentsInChildren<Renderer>(true))
            {
                string shader = renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "";
                bool fluid = shader.EndsWith("BFX_Blood");
                bool decal = shader.EndsWith("BFX_Decal");
                if (!fluid && !decal)
                    continue;

                renderer.transform.localScale *= fluid ? _config.fluidScale : _config.decalScale;

                if (decal)
                {
                    // BFX_ShaderProperies gave the decal a material of its own in Awake, which ran
                    // on Instantiate. A property block here would blank the decal's other
                    // instanced values (it drew black), so the tint goes on that copy instead.
                    renderer.sharedMaterial.SetColor(DecalColor, _config.decalColor);
                    continue;
                }

                renderer.GetPropertyBlock(block);
                block.SetColor(FluidColor, _config.fluidColor);
                renderer.SetPropertyBlock(block);
                block.Clear();
            }

            Object.Destroy(blood, _config.lifetime);
        }
    }
}
