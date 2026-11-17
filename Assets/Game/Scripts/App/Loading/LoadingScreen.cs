using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The core's loading screen (uGUI): a CanvasGroup that blacks the screen out and blocks raycasts while shown, and
    /// a progress bar — an Image with fillAmount or a Slider, whichever is set. Fades on unscaled time. A child of the
    /// App prefab, under a Canvas sorted above everything (the canvas lives as long as the app).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour, ILoadingScreen
    {
        [Tooltip("The whole screen: alpha is the fade, blocksRaycasts keeps input off the scene while shown")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("Progress bar as a filled Image (Image Type: Filled); may be empty")]
        [SerializeField] private Image _barFill;
        [Tooltip("Progress bar as a Slider 0..1; may be empty")]
        [SerializeField] private Slider _barSlider;
        [Tooltip("Seconds of a full fade in or out, real time")]
        [SerializeField] private float _fadeSeconds = 0.25f;

        private float _targetAlpha;

        public bool IsShown { get; private set; }
        public bool IsCovering => IsShown && (_group == null || _group.alpha >= 1f);

        public void Show(bool instant)
        {
            IsShown = true;
            _targetAlpha = 1f;
            SetProgress(0f);
            if (_group == null)
                return;

            _group.blocksRaycasts = true;
            _group.interactable = true;
            if (instant || _fadeSeconds <= 0f)
                _group.alpha = 1f;
        }

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (_barFill != null)
                _barFill.fillAmount = value;
            if (_barSlider != null)
                _barSlider.SetValueWithoutNotify(value);
        }

        public void Hide()
        {
            IsShown = false;
            _targetAlpha = 0f;
            if (_group == null)
                return;

            _group.blocksRaycasts = false;
            _group.interactable = false;
            if (_fadeSeconds <= 0f)
                _group.alpha = 0f;
        }

        private void Awake()
        {
            // Hidden until the flow asks; the Boot scene shows it instantly anyway
            if (_group != null && !IsShown)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
        }

        private void Update() // perf-ok: ticks during pause (loading screen fade)
        {
            // Exact compare: MoveTowards lands on the target, and IsCovering waits for alpha 1 exactly
            if (_group == null || _group.alpha == _targetAlpha)
                return;

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime / _fadeSeconds);
        }
    }
}
