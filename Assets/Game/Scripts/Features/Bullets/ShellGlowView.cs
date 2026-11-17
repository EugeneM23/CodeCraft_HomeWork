using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: the tank shell as a glowing box, like a voxel slug. The box is an unlit HDR
    /// colour (brighter than 1, so Bloom picks it up); its trail takes the same colour. Every field
    /// applies live in the inspector, in Play too.
    /// </summary>
    [ExecuteAlways]
    public sealed class ShellGlowView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _box;
        [SerializeField] private Renderer _boxRenderer;
        [SerializeField] private TrailRenderer _trail;

        [Tooltip("Width, height and length (m) of the box.")]
        [SerializeField] private Vector3 _size = new(0.1f, 0.1f, 0.36f);
        [ColorUsage(false, false)]
        [SerializeField] private Color _glowColor = new(1f, 0.55f, 0.18f, 1f);
        [Tooltip("Brightness of the box: above 1 it blooms.")]
        [SerializeField] private float _glowIntensity = 8f;

        [Header("Trail")]
        [SerializeField] private bool _trailOn = true;
        [Tooltip("Seconds the trail stays behind the shell.")]
        [SerializeField] private float _trailTime = 0.12f;
        [Tooltip("Width (m) of the trail at the shell.")]
        [SerializeField] private float _trailWidth = 0.09f;

        private MaterialPropertyBlock _block;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (_box != null)
                _box.localScale = _size;

            if (_boxRenderer != null)
            {
                _block ??= new MaterialPropertyBlock();
                _boxRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, _glowColor * _glowIntensity);
                _boxRenderer.SetPropertyBlock(_block);
            }

            if (_trail != null)
            {
                _trail.enabled = _trailOn;
                _trail.time = _trailTime;
                _trail.widthMultiplier = _trailWidth / 0.12f; // The width curve starts at 0.12.
                Color head = _glowColor;
                Color tail = _glowColor * 0.6f;
                tail.a = 0f;
                _trail.startColor = head;
                _trail.endColor = tail;
            }
        }
    }
}
